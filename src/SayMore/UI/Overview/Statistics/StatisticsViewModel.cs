using System;
using System.Linq;
using System.Collections.Generic;
using SayMore.Model;
using SayMore.Model.Files;
using SayMore.Model.Files.DataGathering;
using SayMore.UI.Charts;

namespace SayMore.UI.Overview.Statistics
{
	public class StatisticsViewModel : IDisposable
	{
		public event EventHandler NewStatisticsAvailable;
		public event EventHandler FinishedGatheringStatisticsForAllFiles;

		private readonly IEnumerable<ComponentRole> _componentRoles;
		private readonly AudioVideoDataGatherer _backgroundStatisticsGather;
		protected HTMLChartBuilder _chartBuilder;

		public PersonInformant PersonInformant { get; protected set; }
		public EventWorkflowInformant EventInformant { get; protected set; }
		public string ProjectName { get; protected set; }
		public string ProjectPath { get; protected set; }

		/// ------------------------------------------------------------------------------------
		public StatisticsViewModel(Project project, PersonInformant personInformant,
			EventWorkflowInformant eventInformant, IEnumerable<ComponentRole> componentRoles,
			AudioVideoDataGatherer backgroundStatisticsMananager)
		{
			ProjectName = (project == null ? string.Empty : project.Name);
			ProjectPath = (project == null ? string.Empty : project.FolderPath);
			PersonInformant = personInformant;
			EventInformant = eventInformant;
			_componentRoles = componentRoles;
			_backgroundStatisticsGather = backgroundStatisticsMananager;
			_backgroundStatisticsGather.NewDataAvailable += HandleNewStatistics;
			_backgroundStatisticsGather.FinishedProcessingAllFiles += HandleFinishedGatheringStatisticsForAllFiles;

			_chartBuilder = new HTMLChartBuilder(this);
		}

		/// ------------------------------------------------------------------------------------
		public void Dispose()
		{
			_backgroundStatisticsGather.NewDataAvailable -= HandleNewStatistics;
			_backgroundStatisticsGather.FinishedProcessingAllFiles -= HandleFinishedGatheringStatisticsForAllFiles;
		}

		/// ------------------------------------------------------------------------------------
		public string Status
		{
			get { return _backgroundStatisticsGather.Status; }
		}

		/// ------------------------------------------------------------------------------------
		public bool IsDataUpToDate
		{
			get { return _backgroundStatisticsGather.DataUpToDate; }
		}

		/// ------------------------------------------------------------------------------------
		public bool IsBusy
		{
			get { return _backgroundStatisticsGather.Busy; }
		}

		/// ------------------------------------------------------------------------------------
		public string HTMLString
		{
			get { return _chartBuilder.GetStatisticsCharts(); }
		}

		/// ------------------------------------------------------------------------------------
		public IEnumerable<KeyValuePair<string, string>> GetElementStatisticsPairs()
		{
			yield return new KeyValuePair<string, string>("Events:",
				EventInformant.NumberOfEvents.ToString());

			yield return new KeyValuePair<string, string>("People:",
				PersonInformant.NumberOfPeople.ToString());
		}

		/// ------------------------------------------------------------------------------------
		public IEnumerable<ComponentRoleStatistics> GetComponentRoleStatisticsPairs()
		{
			foreach (var role in _componentRoles.Where(def => def.MeasurementType == ComponentRole.MeasurementTypes.Time))
			{
				long bytes = GetTotalComponentRoleFileSizes(role);
				var size = (bytes == 0 ? "---" : ComponentFile.GetDisplayableFileSize(bytes, false));

				yield return new ComponentRoleStatistics
				{
					Name = role.Name,
					Length = GetRecordingDurations(role).ToString(),
					Size = size
				};
			}
		}

		/// ------------------------------------------------------------------------------------
		private long GetTotalComponentRoleFileSizes(ComponentRole role)
		{
			long bytes = 0;
			foreach (MediaFileInfo info in _backgroundStatisticsGather.GetAllFileData())
			{
				if (role.IsMatch(info.MediaFilePath))
					bytes += info.LengthInBytes;
			}

			return bytes;
		}

		/// ------------------------------------------------------------------------------------
		public TimeSpan GetRecordingDurations(ComponentRole role)
		{
			var total = TimeSpan.Zero;
			foreach (MediaFileInfo info in _backgroundStatisticsGather.GetAllFileData())
			{
				if (role.IsMatch(info.MediaFilePath))
					total += info.Duration;
			}

			// Trim off the milliseconds so it doesn't get too geeky
			return new TimeSpan(total.Hours, total.Minutes, total.Seconds);
		}

		/// ------------------------------------------------------------------------------------
		public void Refresh()
		{
			_backgroundStatisticsGather.Restart();
		}

		/// ------------------------------------------------------------------------------------
		void HandleNewStatistics(object sender, EventArgs e)
		{
			if (NewStatisticsAvailable != null)
				NewStatisticsAvailable(this, EventArgs.Empty);
		}

		/// ------------------------------------------------------------------------------------
		void HandleFinishedGatheringStatisticsForAllFiles(object sender, EventArgs e)
		{
			if (FinishedGatheringStatisticsForAllFiles != null)
				FinishedGatheringStatisticsForAllFiles(this, EventArgs.Empty);
		}
	}

	public class ComponentRoleStatistics
	{
		public string Name { get; set; }
		public string Length { get; set; }
		public string Size { get; set; }
	}
}