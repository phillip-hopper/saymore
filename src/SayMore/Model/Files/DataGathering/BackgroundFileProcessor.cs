using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ThreadState = System.Threading.ThreadState;

namespace SayMore.Model.Files.DataGathering
{
	/// <summary>
	/// This is the base class for processes which live in the background,
	/// gathering data about the files in the collection so that this data
	/// is quickly accesible when needed.
	/// </summary>
	public abstract class BackgroundFileProcessor<T>: IDisposable where T: class
	{
		private Thread _workerThread;
		private string _rootDirectoryPath;
		private readonly IEnumerable<FileType> _typesOfFilesToProcess;
		private readonly Func<string, T> _fileDataFactory;
		private bool _restartRequested = true;
		protected Dictionary<string, T> _fileToDataDictionary = new Dictionary<string, T>();
		private Queue<FileSystemEventArgs> _pendingFileEvents;

		public BackgroundFileProcessor(string rootDirectoryPath, IEnumerable<FileType> typesOfFilesToProcess, Func<string, T> fileDataFactory)
		{
			_rootDirectoryPath = rootDirectoryPath;
			_typesOfFilesToProcess = typesOfFilesToProcess;
			_fileDataFactory = fileDataFactory;
			Status = "Not yet started";
			_pendingFileEvents = new Queue<FileSystemEventArgs>();
		}

		protected virtual bool GetDoIncludeFile(string path)
		{
			return _typesOfFilesToProcess.Any(t => t.IsMatch(path));
		}

		public void Start()
		{
			_workerThread = new Thread(StartWorking);
			_workerThread.Name = GetType().Name;
			_workerThread.Priority = ThreadPriority.Lowest;
			_workerThread.Start();
		}

		private void StartWorking()
		{
			Status = "Working";//NB: this helps simplify unit tests, if go to the busy state before returning

			using (var watcher = new FileSystemWatcher(_rootDirectoryPath))
			{
				watcher.Created += new FileSystemEventHandler(QueueFileEvent);

				watcher.Renamed += new RenamedEventHandler(QueueFileEvent);
				watcher.Changed += new FileSystemEventHandler(QueueFileEvent);

				watcher.EnableRaisingEvents = true;
				//now just wait for file events that we should handle (on a different thread, in response to events)

				while (!ShouldStop)
				{
					if (_restartRequested)
					{
						_restartRequested = false;
						ProcessAllFiles();
					}
					lock (_pendingFileEvents)
					{
						if (_pendingFileEvents.Count > 0)
						{
							ProcessFileEvent(_pendingFileEvents.Dequeue());
							break;
						}
					}
					if (!ShouldStop)
						Thread.Sleep(100);
				}
			}
		}

		private void QueueFileEvent(object sender, FileSystemEventArgs e)
		{
			lock(_pendingFileEvents)
			{
				_pendingFileEvents.Enqueue(e);
			}
		}

		private void ProcessFileEvent(FileSystemEventArgs fileEvent)
		{
			try
			{
				if (fileEvent is RenamedEventArgs)
				{
					var e = fileEvent as RenamedEventArgs;
					lock (((ICollection) _fileToDataDictionary).SyncRoot)
					{
						if (_fileToDataDictionary.ContainsKey(e.OldFullPath.ToLower()))
						{
							_fileToDataDictionary.Add(e.FullPath.ToLower(), _fileToDataDictionary[e.OldFullPath.ToLower()]);
							_fileToDataDictionary.Remove(e.OldFullPath.ToLower());
						}
					}
				}
				else
				{
					Status = "Working";
					CollectDataForFile(fileEvent.FullPath);
					Status = "Up to date";
				}
			}
			catch (Exception)
			{
#if DEBUG
				//nothing here is worth crashing over in release build
				throw;
#endif
			}
		}


		public T GetFileData(string filePath)
		{
			T stats;
			lock (((ICollection)_fileToDataDictionary).SyncRoot)
			{
				if (_fileToDataDictionary.TryGetValue(filePath.ToLower(), out stats))
					return stats;
				return null;
			}
		}

		private void CollectDataForFile(string path)
		{
			try
			{
				if (!GetDoIncludeFile(path))
				{
					return;
				}
				Debug.WriteLine("processing " + path);
				if (!ShouldStop)
				{
					var fileData = _fileDataFactory(path);

					lock (((ICollection)_fileToDataDictionary).SyncRoot)
					{
						if (_fileToDataDictionary.ContainsKey(path.ToLower()))
						{
							_fileToDataDictionary.Remove(path.ToLower());
						}
						_fileToDataDictionary.Add(path.ToLower(), fileData);
					}
				}
			}
			catch (Exception e)
			{
				Debug.WriteLine(e.Message);
#if  DEBUG
				Palaso.Reporting.ErrorReport.NotifyUserOfProblem(e, "Error gathering data");
#endif
				//nothing here is worth crashing over
			}
			InvokeNewDataAvailable();
		}

		public IEnumerable<T> GetAllFileData()
		{
			lock (((ICollection)_fileToDataDictionary).SyncRoot)
			{
				//Give a copy (a list) because if we just give an enumerator, we'll get
				//an error if/when this collection is altered on another thread
				return _fileToDataDictionary.Values.ToList();
			}
		}

		public void ProcessAllFiles()
		{
			Status = "Working";

			//now that the watcher is up and running, gather up all existing files
			lock (((ICollection)_fileToDataDictionary).SyncRoot)
			{
				_fileToDataDictionary.Clear();
			}
			var paths = new List<string>();
			paths.AddRange(Directory.GetFiles(_rootDirectoryPath, "*.*", SearchOption.AllDirectories));

			for (int i = 0; i < paths.Count; i++)
			{
				if (ShouldStop)
					break;
				Status = string.Format("Processing {0} of {1} files", 1 + i, paths.Count);
				if (ShouldStop)
					break;
				CollectDataForFile(paths[i]);
			}
			Status = "Up to date";
		}

		public void Restart()
		{
			_restartRequested = true;
		}

		protected bool ShouldStop
		{
			get
			{
				return (Thread.CurrentThread.ThreadState &
						(ThreadState.StopRequested | ThreadState.AbortRequested | ThreadState.Stopped | ThreadState.Aborted)) > 0;
			}
		}

		public bool Busy
		{
			get { return Status == "Working"; }
		}

		public string Status
		{
			//enhance: provide an enumeration for the use of tests (at least)
			get;
			private set;
		}

		public void Dispose()
		{
			if (_workerThread != null)
			{
				_workerThread.Abort();//will eventually lead to it stopping
			}
			_workerThread = null;
		}


		public event EventHandler NewDataAvailable;

		protected void InvokeNewDataAvailable()
		{
			EventHandler handler = NewDataAvailable;
			if (handler != null) handler(this, null);
		}
	}

}