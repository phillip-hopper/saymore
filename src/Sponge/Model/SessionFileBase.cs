using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace SIL.Sponge.Model
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Provides a base class for information about a single file (e.g. name, icon, type, size
	/// etc.)
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class SessionFileBase
	{
		#region Windows API stuff
#if !MONO
		public const uint SHGFI_DISPLAYNAME = 0x00000200;
		public const uint SHGFI_TYPENAME = 0x400;
		public const uint SHGFI_EXETYPE = 0x2000;
		public const uint SHGFI_ICON = 0x100;
		public const uint SHGFI_LARGEICON = 0x0; // 'Large icon
		public const uint SHGFI_SMALLICON = 0x1; // 'Small icon

		[DllImport("shell32.dll")]
		public static extern IntPtr SHGetFileInfo(string pszPath, uint
			dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);
#endif
		#endregion

		private string m_fullFilePath;

		#region Constructors
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="SessionFile"/> class.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public SessionFileBase()
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="SessionFile"/> class.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public SessionFileBase(string fullFilePath) : this()
		{
			FullFilePath = fullFilePath;
		}

		#endregion



		#region Properties
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the file (this should include the full path).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public string FullFilePath
		{
			get { return m_fullFilePath; }
			set
			{
				m_fullFilePath = value;
				DateModified = File.GetLastWriteTime(m_fullFilePath).ToString();
				var icon = Icon.ExtractAssociatedIcon(m_fullFilePath);
				LargeIcon = new Bitmap(icon.ToBitmap());
				var fi = new FileInfo(m_fullFilePath);
				FileSize = GetDisplayableFileSize(fi.Length);
				SetSmallIconAndFileType();
			}
		}

		[XmlIgnore]
		public string FileType { get; private set; }

		[XmlIgnore]
		public string FileSize { get; private set; }

		[XmlIgnore]
		public Bitmap LargeIcon { get; private set; }

		[XmlIgnore]
		public Bitmap SmallIcon { get; private set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the name of the file (without its path).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public string FileName
		{
			get { return Path.GetFileName(FullFilePath); }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the file's folder (i.e. full path without file name).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public string Folder
		{
			get { return Path.GetDirectoryName(FullFilePath); }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the last time the session's file was modified.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public string DateModified { get; private set; }

		#endregion

		#region Methods for initializing some info.
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the small icon.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void SetSmallIconAndFileType()
		{
#if !MONO
			SHFILEINFO shinfo = new SHFILEINFO();
			SHGetFileInfo(m_fullFilePath, 0, ref
				shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_TYPENAME |
				SHGFI_SMALLICON | SHGFI_ICON | SHGFI_DISPLAYNAME);

			SmallIcon = Icon.FromHandle(shinfo.hIcon).ToBitmap();
			FileType = shinfo.szTypeName;
#else
			// REVIEW: Figure out a better way to get this in Mono.
			Icon icon = Icon.ExtractAssociatedIcon(m_fileName);
			var largeIcons = new ImageList();
			largeIcons.Images.Add(icon);
			var bmSmall = new Bitmap(16, 16);

			using (var g = Graphics.FromImage(bmSmall))
			{
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				g.DrawImage(LargeIcon, new Rectangle(0, 0, 16, 16),
					new Rectangle(new Point(0, 0), LargeIcon.Size), GraphicsUnit.Pixel);
			}

			SmallIcon = bmSmall;
			// TODO: Figure out how to get FileType in Mono.
#endif
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Sets the size of the session file in a displayable form.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private static string GetDisplayableFileSize(long fileSize)
		{
			if (fileSize < 1000)
				return string.Format("{0} B", fileSize);

			if (fileSize < Math.Pow(1024, 2))
			{
				var size = (int)Math.Round(fileSize / (decimal)1024, 2, MidpointRounding.AwayFromZero);
				if (size < 1)
					size = 1;

				return string.Format("{0} KB", size.ToString("###"));
			}

			double dblSize;
			if (fileSize < Math.Pow(1024, 3))
			{
				dblSize = Math.Round(fileSize / Math.Pow(1024, 2), 2, MidpointRounding.AwayFromZero);
				return string.Format("{0} MB", dblSize.ToString("###.##"));
			}

			dblSize = Math.Round(fileSize / Math.Pow(1024, 3), 2, MidpointRounding.AwayFromZero);
			return string.Format("{0} GB", dblSize.ToString("###,###.##"));
		}

		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns a <see cref="System.String"/> that represents this instance.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public override string ToString()
		{
			return FileName;
		}
	}
}