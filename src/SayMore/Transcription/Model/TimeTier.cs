using System;
using System.IO;
using System.Linq;
using SayMore.Properties;
using SayMore.Transcription.UI;

namespace SayMore.Transcription.Model
{
	public enum BoundaryModificationResult
	{
		Success,
		SegmentNotFound,
		SegmentWillBeTooShort,
		NextSegmentWillBeTooShort
	}

	/// ----------------------------------------------------------------------------------------
	public class TimeTier : TierBase
	{
		public string MediaFileName { get; protected set; }
		public Action<string> BackupOralAnnotationSegmentFileAction { get; set; }

		/// ------------------------------------------------------------------------------------
		public TimeTier(string filename) : this("Original", filename)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// The segmentFileFolder is used for renaming (due to segment boundary changes) and
		/// removing audio segment annotation files that are being created/modified in a
		/// temp. location.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public TimeTier(string id, string filename) : base(id, tier => new AudioWaveFormColumn(tier))
		{
			MediaFileName = filename;
		}

		/// ------------------------------------------------------------------------------------
		protected override TierBase GetNewTierInstance()
		{
			return new TimeTier(Id, MediaFileName);
		}

		#region Methods for getting segment file paths
		/// ------------------------------------------------------------------------------------
		public string GetFullPathToCarefulSpeechFile(Segment segment)
		{
			return Path.Combine(SegmentFileFolder, ComputeFileNameForCarefulSpeechSegment(segment));
		}

		/// ------------------------------------------------------------------------------------
		public string GetFullPathToOralTranslationFile(Segment segment)
		{
			return Path.Combine(SegmentFileFolder, ComputeFileNameForOralTranslationSegment(segment));
		}

		#endregion

		#region Static methods for computing oral annotation segment audio file names.
		/// ------------------------------------------------------------------------------------
		public static string ComputeFileNameForCarefulSpeechSegment(Segment segment)
		{
			return ComputeFileNameForCarefulSpeechSegment(segment.Start, segment.End);
		}

		/// ------------------------------------------------------------------------------------
		public static string ComputeFileNameForOralTranslationSegment(Segment segment)
		{
			return ComputeFileNameForOralTranslationSegment(segment.Start, segment.End);
		}

		/// ------------------------------------------------------------------------------------
		public static string ComputeFileNameForCarefulSpeechSegment(float start, float end)
		{
			return string.Format("{0}_to_{1}{2}", start, end,
				Settings.Default.OralAnnotationCarefulSegmentFileSuffix);
		}

		/// ------------------------------------------------------------------------------------
		public static string ComputeFileNameForOralTranslationSegment(float start, float end)
		{
			return string.Format("{0}_to_{1}{2}", start, end,
				Settings.Default.OralAnnotationTranslationSegmentFileSuffix);
		}

		#endregion

		#region Properties
		/// ------------------------------------------------------------------------------------
		public override string DisplayName
		{
			get { return string.Empty; }
		}

		/// ------------------------------------------------------------------------------------
		public string SegmentFileFolder
		{
			get { return MediaFileName + Settings.Default.OralAnnotationsFolderSuffix; }
		}

		/// ------------------------------------------------------------------------------------
		public override TierType TierType
		{
			get { return TierType.Time; }
			set { }
		}

		#endregion

		/// ------------------------------------------------------------------------------------
		public int GetIndexOfSegment(Segment segment)
		{
			for (int i = 0; segment != null && i < Segments.Count; i++)
			{
				if (Segments[i].TimeRange == segment.TimeRange)
					return i;
			}

			return -1;
		}

		/// ------------------------------------------------------------------------------------
		public Segment GetSegmentHavingEndBoundary(float endBoundary)
		{
			return Segments.FirstOrDefault(s => s.End.Equals(endBoundary));
		}

		/// ------------------------------------------------------------------------------------
		public Segment GetSegmentEnclosingTime(float time)
		{
			return Segments.FirstOrDefault(s => s.TimeRange.GetIsTimeInRange(time, false, true));
		}

		#region Methods for Adding and removing segments
		/// ------------------------------------------------------------------------------------
		public Segment AddSegment(float start, float stop)
		{
			var segment = new Segment(this, start, stop);
			Segments.Add(segment);
			return segment;
		}

		/// ------------------------------------------------------------------------------------
		public bool RemoveSegmentHavingEndBoundary(float endBoundary)
		{
			var segment = GetSegmentHavingEndBoundary(endBoundary);
			return segment != null && RemoveSegment(segment);
		}

		/// ------------------------------------------------------------------------------------
		public bool RemoveSegment(Segment segment)
		{
			return RemoveSegment(GetIndexOfSegment(segment));
		}

		/// ------------------------------------------------------------------------------------
		public override bool RemoveSegment(int index)
		{
			if (Segments.Count > 0 && index >= 0 && index < Segments.Count)
			{
				var segToRemove = Segments[index];

				if (Segments.Count > 1 && index < Segments.Count - 1)
				{
					var nextSeg = Segments[index + 1];
					RenameAnnotationSegmentFile(nextSeg, segToRemove.Start, nextSeg.End);
					nextSeg.Start = segToRemove.Start;
				}

				DeleteAnnotationSegmentFile(segToRemove);
			}

			return base.RemoveSegment(index);
		}

		#endregion

		/// ------------------------------------------------------------------------------------
		public BoundaryModificationResult ChangeSegmentsEndBoundary(float oldEndBoundary, float newEndBoundary)
		{
			var segment = GetSegmentHavingEndBoundary(oldEndBoundary);

			return (segment == null ? BoundaryModificationResult.SegmentNotFound :
				ChangeSegmentsEndBoundary(segment, newEndBoundary));
		}

		/// ------------------------------------------------------------------------------------
		public BoundaryModificationResult ChangeSegmentsEndBoundary(Segment segment, float newEndBoundary)
		{
			// New boundary must be at least 1/2 second greater than the segment's start boundary.
			if (segment.Start >= newEndBoundary - 0.5f)
				return BoundaryModificationResult.SegmentWillBeTooShort;

			var segIndex = GetIndexOfSegment(segment);
			if (segIndex < 0)
				return BoundaryModificationResult.SegmentNotFound;

			var nextSegment = (segIndex < Segments.Count - 1 ? Segments[segIndex + 1] : null);

			if (nextSegment != null)
			{
				if (newEndBoundary + 0.5f >= nextSegment.End)
					return BoundaryModificationResult.NextSegmentWillBeTooShort;

				RenameAnnotationSegmentFile(nextSegment, newEndBoundary, nextSegment.End);
				nextSegment.Start = newEndBoundary;
			}

			RenameAnnotationSegmentFile(Segments[segIndex], Segments[segIndex].Start, newEndBoundary);
			Segments[segIndex].End = newEndBoundary;

			return BoundaryModificationResult.Success;
		}

		/// ------------------------------------------------------------------------------------
		public BoundaryModificationResult InsertSegmentBoundary(float newBoundary)
		{
			float newSegStart = 0f;
			var segBeingSplit = Segments.FirstOrDefault(seg => newBoundary > seg.Start && newBoundary <= seg.End);

			if (segBeingSplit == null)
			{
				if (Segments.GetLast() != null)
					newSegStart = Segments.GetLast().End;

				if (newSegStart >= newBoundary - 0.5f)
					return BoundaryModificationResult.SegmentWillBeTooShort;

				AddSegment(newSegStart, newBoundary);
				return BoundaryModificationResult.Success;
			}

			if (segBeingSplit.Start >= newBoundary - 0.5f)
				return BoundaryModificationResult.SegmentWillBeTooShort;

			if (newBoundary + 0.5f >= segBeingSplit.End)
				return BoundaryModificationResult.NextSegmentWillBeTooShort;

			RenameAnnotationSegmentFile(segBeingSplit, segBeingSplit.Start, newBoundary);

			float newSegEnd = segBeingSplit.End;
			segBeingSplit.End = newBoundary;
			var newSegment = new Segment(segBeingSplit.Tier, newBoundary, newSegEnd);
			Segments.Insert(GetIndexOfSegment(segBeingSplit) + 1, newSegment);

			return BoundaryModificationResult.Success;
		}

		#region Methods for renaming and deleting oral annotation segment files
		/// ------------------------------------------------------------------------------------
		public void RenameAnnotationSegmentFile(Segment oldSegment, float newStart, float newEnd)
		{
			try
			{
				var oldSegmentFilePath = Path.Combine(SegmentFileFolder,
					ComputeFileNameForCarefulSpeechSegment(oldSegment));

				if (File.Exists(oldSegmentFilePath))
				{
					if (BackupOralAnnotationSegmentFileAction != null)
						BackupOralAnnotationSegmentFileAction(oldSegmentFilePath);

					File.Move(oldSegmentFilePath, Path.Combine(SegmentFileFolder,
						ComputeFileNameForCarefulSpeechSegment(newStart, newEnd)));
				}
			}
			catch { }

			try
			{
				var oldSegmentFilePath = Path.Combine(SegmentFileFolder,
					ComputeFileNameForOralTranslationSegment(oldSegment));

				if (File.Exists(oldSegmentFilePath))
				{
					if (BackupOralAnnotationSegmentFileAction != null)
						BackupOralAnnotationSegmentFileAction(oldSegmentFilePath);

					File.Move(oldSegmentFilePath, Path.Combine(SegmentFileFolder,
						ComputeFileNameForOralTranslationSegment(newStart, newEnd)));
				}
			}
			catch { }
		}

		/// ------------------------------------------------------------------------------------
		public void DeleteAnnotationSegmentFile(Segment segment)
		{
			try
			{
				var path = Path.Combine(SegmentFileFolder, ComputeFileNameForCarefulSpeechSegment(segment));
				if (File.Exists(path))
				{
					if (BackupOralAnnotationSegmentFileAction != null)
						BackupOralAnnotationSegmentFileAction(path);

					File.Delete(path);
				}
			}
			catch { }

			try
			{
				var path = Path.Combine(SegmentFileFolder, ComputeFileNameForOralTranslationSegment(segment));
				if (File.Exists(path))
				{
					if (BackupOralAnnotationSegmentFileAction != null)
						BackupOralAnnotationSegmentFileAction(path);

					File.Delete(path);
				}
			}
			catch { }
		}

		#endregion

		#region Methods for determining whether or not a boundary can move left or right
		/// ------------------------------------------------------------------------------------
		public bool CanBoundaryMoveLeft(float boundaryToMove, float secondsToMove)
		{
			var newBoundary = boundaryToMove - secondsToMove;
			var segment = GetSegmentEnclosingTime(boundaryToMove);

			return (newBoundary > 0 && (segment == null || newBoundary > segment.Start + 0.5f));
		}

		/// ------------------------------------------------------------------------------------
		public bool CanBoundaryMoveRight(float boundaryToMove, float secondsToMove, float limit)
		{
			var newBoundary = boundaryToMove + secondsToMove;
			if (newBoundary <= 0 || newBoundary > limit)
				return false;

			var segment = GetSegmentHavingEndBoundary(boundaryToMove);
			if (segment != null)
			{
				int i = GetIndexOfSegment(segment);
				return (i == Segments.Count - 1 || newBoundary <= Segments[i + 1].End - 0.5f);
			}

			segment = GetSegmentEnclosingTime(boundaryToMove);
			return (segment == null || newBoundary <= segment.End - 0.5f);
		}

		#endregion
	}
}
