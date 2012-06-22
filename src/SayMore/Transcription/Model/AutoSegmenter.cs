using System;
using System.Collections.Generic;
using Palaso.Progress;
using SayMore.Media.Audio;
using SayMore.Model.Files;
using SayMore.Properties;

namespace SayMore.Transcription.Model
{
	public class AutoSegmenter
	{
		private readonly ComponentFile _file;
		private IWaveStreamReader _streamReader;

		private uint _adjacentSamplesToFactorIntoAdjustedScore;

		/// ------------------------------------------------------------------------------------
		public AutoSegmenter(ComponentFile file)
		{
			_file = file;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor to facilitate testing
		/// </summary>
		/// ----------------------------------------------------------------------------------------
		public AutoSegmenter(IWaveStreamReader sampleProvider)
		{
			_streamReader = sampleProvider;
		}

		/// ------------------------------------------------------------------------------------
		private IWaveStreamReader StreamReader
		{
			get
			{
				if (_streamReader == null)
					_streamReader = new WaveFileReaderWrapper(_file.PathToAnnotatedFile);
				return _streamReader;
			}
		}

		/// ------------------------------------------------------------------------------------
		public string Run()
		{
			if (_file.GetAnnotationFile() != null)
				return _file.GetAnnotationFile().FileName; // REVIEW: This probably shouldn't happen. Maybe throw an exception.

			WaitCursor.Show();
			var tiers = new TierCollection(_file.PathToAnnotatedFile);

			var timeTier = tiers.GetTimeTier();

			if (timeTier == null)
			{
				timeTier = new TimeTier(_file.PathToAnnotatedFile);
				tiers.Insert(0, timeTier);
			}

			foreach (var segment in GetNaturalBreaks())
			{
				timeTier.AppendSegment((float)segment.TotalSeconds);
			}

			WaitCursor.Hide();

			return tiers.Save();
		}

		/// ------------------------------------------------------------------------------------
		public IEnumerable<TimeSpan> GetNaturalBreaks()
		{
			uint requestedSamples = (uint)StreamReader.TotalTime.TotalMilliseconds;
			//(StreamReader.SampleCount > uint.MaxValue) ? uint.MaxValue : (uint)StreamReader.SampleCount;
			if (requestedSamples > 0)
			{
				var samples = AudioFileHelper.GetSamples(StreamReader, requestedSamples);
				uint remainingSamples = (uint)samples.GetLength(0);

				uint lastBreak = 0;
				var millisecondsPerSample = StreamReader.TotalTime.TotalMilliseconds / remainingSamples;
				_adjacentSamplesToFactorIntoAdjustedScore = (uint)(Settings.Default.AutoSegmenterPreferrerdPauseLength.TotalMilliseconds / millisecondsPerSample);
				var minSamplesPerSegment = (uint)(Settings.Default.MinimumSegmentLengthInMilliseconds / millisecondsPerSample);
				var maxSamplesPerSegment = (uint)(Settings.Default.AutoSegmenterMaximumSegmentLengthInMilliseconds / millisecondsPerSample);

				uint idealSegmentLengthInSamples = (minSamplesPerSegment + maxSamplesPerSegment) / 2;

				while (remainingSamples >= maxSamplesPerSegment)
				{
					if (remainingSamples < idealSegmentLengthInSamples * 2)
						idealSegmentLengthInSamples = remainingSamples / 2;
					uint samplesOnEitherSideOfTarget = idealSegmentLengthInSamples + _adjacentSamplesToFactorIntoAdjustedScore - minSamplesPerSegment;
					uint targetBreak = lastBreak + idealSegmentLengthInSamples;
					uint bestBreak = targetBreak;
					double[] rawScores = new double[idealSegmentLengthInSamples * 2 + 1];
					double[] adjustedScores = new double[idealSegmentLengthInSamples * 2 + 1];
					rawScores[idealSegmentLengthInSamples] = ComputeRawScore(samples, targetBreak);
					double bestScore = double.MaxValue;
					double averageScore = 0;
					for (uint i = 1; i < samplesOnEitherSideOfTarget; i++)
					{
						if (i < idealSegmentLengthInSamples)
						{
							rawScores[idealSegmentLengthInSamples + i] = ComputeRawScore(samples, targetBreak + i);
							rawScores[idealSegmentLengthInSamples - i] = ComputeRawScore(samples, targetBreak - i);
						}
						if (i >= _adjacentSamplesToFactorIntoAdjustedScore)
						{
// DEBUG code if ((targetBreak - i) * millisecondsPerSample < 138939 && lastBreak > 138939 - maxSamplesPerSegment)
							//{
							//    System.Diagnostics.Debug.WriteLine("Position = " + (lastBreak + idealSegmentLengthInSamples + i) + "; Raw score = " + rawScores[idealSegmentLengthInSamples + i]);
							//    System.Diagnostics.Debug.WriteLine("Position = " + (lastBreak + idealSegmentLengthInSamples - i) + "; Raw score = " + rawScores[idealSegmentLengthInSamples - i]);
							//}
							double distanceFactor = Math.Pow(i * Settings.Default.AutoSegmenterOptimumLengthClampingFactor + 1, 2);
							uint iAdjust = idealSegmentLengthInSamples + i - _adjacentSamplesToFactorIntoAdjustedScore;
							double totalNewAdjustedScores = adjustedScores[iAdjust] = ComputeAdjustedScore(rawScores, iAdjust, distanceFactor);
							if (adjustedScores[iAdjust] < bestScore)
							{
								bestScore = adjustedScores[iAdjust];
								bestBreak = lastBreak + iAdjust;
							}
							iAdjust = idealSegmentLengthInSamples - i + _adjacentSamplesToFactorIntoAdjustedScore;
							adjustedScores[iAdjust] = ComputeAdjustedScore(rawScores, iAdjust, distanceFactor);
							totalNewAdjustedScores += adjustedScores[iAdjust];
							if (adjustedScores[iAdjust] < bestScore)
							{
								bestScore = adjustedScores[iAdjust];
								bestBreak = lastBreak + iAdjust;
							}
							uint samplesInPrevAvg = (1 + 2 * ( i - _adjacentSamplesToFactorIntoAdjustedScore - 1));
							averageScore = (averageScore * samplesInPrevAvg + totalNewAdjustedScores) / (samplesInPrevAvg + 2);
							if (bestScore < averageScore / 2 && i < idealSegmentLengthInSamples &&
								rawScores[idealSegmentLengthInSamples + i] < rawScores[idealSegmentLengthInSamples + i + 1] &&
								rawScores[idealSegmentLengthInSamples - i] < rawScores[idealSegmentLengthInSamples - i - 1])
							{
								break;
							}
						}
					}
					remainingSamples -= (bestBreak - lastBreak);
					lastBreak = bestBreak;
					yield return TimeSpan.FromMilliseconds(millisecondsPerSample * bestBreak);
				}

				if (remainingSamples > 0)
					yield return StreamReader.TotalTime;
			}
		}

		/// ------------------------------------------------------------------------------------
		private static double ComputeRawScore(Tuple<float, float>[,] samples, uint targetBreak)
		{
			double score = 0;
			for (int c = 0; c < samples.GetLength(1); c++)
				score += Math.Abs(samples[targetBreak, c].Item1) + Math.Abs(samples[targetBreak, c].Item2);
			return score;
		}

		/// ------------------------------------------------------------------------------------
		private double ComputeAdjustedScore(double[] rawScores, uint iAdjust, double distanceFactor)
		{
			double score = rawScores[iAdjust];
			for (uint i = 1; i <= _adjacentSamplesToFactorIntoAdjustedScore; i++)
			{
				if (rawScores.Length > iAdjust + i)
				{
					score += rawScores[iAdjust + i] *
						(_adjacentSamplesToFactorIntoAdjustedScore - i) / _adjacentSamplesToFactorIntoAdjustedScore;
				}
				// REVIEW: The 0.1 allows us to slightly favor longer segments over shorter ones
				//score += (rawScores[iAdjust - i] + 0.1) * (adjacentSamplesToFactorIntoAdjustedScore - i) / adjacentSamplesToFactorIntoAdjustedScore;
				if (iAdjust >= i)
				{
					score += rawScores[iAdjust - i] *
						(_adjacentSamplesToFactorIntoAdjustedScore - i) / _adjacentSamplesToFactorIntoAdjustedScore;
				}
			}
			return score * distanceFactor;
		}
	}
}