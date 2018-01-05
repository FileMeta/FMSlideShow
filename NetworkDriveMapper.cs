using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace SlideDiscWPF
{
	public class NetworkDriveMapper
	{

		#region DllImport

		[DllImport("mpr.dll")]
		private static extern int WNetAddConnection2W(
			ref structNetResource pstNetRes,
			[MarshalAs(UnmanagedType.LPWStr)]
			string psPassword,
			[MarshalAs(UnmanagedType.LPWStr)]
			string psUsername,
			int piFlags);

		[DllImport("mpr.dll")]
		private static extern int WNetCancelConnection2W(
			[MarshalAs(UnmanagedType.LPWStr)]
			string psName,
			int piFlags, int pfForce);

		[DllImport("mpr.dll")]
		private static extern int WNetConnectionDialog(int phWnd, int piType);
		[DllImport("mpr.dll")]
		private static extern int WNetDisconnectDialog(int phWnd, int piType);
		[DllImport("mpr.dll")]
		private static extern int WNetRestoreConnectionW(int phWnd, string psLocalDrive);

		[StructLayout(LayoutKind.Sequential)]
		private struct structNetResource
		{
			public int iScope;
			public int iType;
			public int iDisplayType;
			public int iUsage;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string sLocalName;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string sRemoteName;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string sComment;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string sProvider;
		}

		private const int RESOURCETYPE_DISK = 0x1;

		//Standard	
		private const int CONNECT_INTERACTIVE = 0x00000008;
		private const int CONNECT_PROMPT = 0x00000010;
		private const int CONNECT_UPDATE_PROFILE = 0x00000001;
		//IE4+
		private const int CONNECT_REDIRECT = 0x00000080;
		//NT5 only
		private const int CONNECT_COMMANDLINE = 0x00000800;
		private const int CONNECT_CMD_SAVECRED = 0x00001000;

		#endregion

		#region Methods

		// Map network drive
		public static void MapDrive(string shareName, string driveName, string psUsername, string psPassword)
		{
			//create struct data
			structNetResource stNetRes = new structNetResource();
			stNetRes.iScope = 2;
			stNetRes.iType = RESOURCETYPE_DISK;
			stNetRes.iDisplayType = 3;
			stNetRes.iUsage = 1;
			stNetRes.sRemoteName = shareName;
			stNetRes.sLocalName = driveName;
			//prepare params
			int iFlags = 0;
			//if (lf_SaveCredentials) { iFlags += CONNECT_CMD_SAVECRED; }
			//if (lf_Persistent) { iFlags += CONNECT_UPDATE_PROFILE; }
			//if (ls_PromptForCredentials) { iFlags += CONNECT_INTERACTIVE + CONNECT_PROMPT; }
			if (string.IsNullOrEmpty(psUsername)) psUsername = null;
			if (string.IsNullOrEmpty(psPassword)) psPassword = null;

			int w32Result = WNetAddConnection2W(ref stNetRes, psPassword, psUsername, iFlags);
			if (w32Result != 0)
			{
				throw new System.ComponentModel.Win32Exception(w32Result);
			}
		}

		// Unmap network drive	
		public static void UnMapDrive(string driveOrShareName, bool force)
		{
			//call unmap and return
			int iFlags = 0;
			//if (lf_Persistent) { iFlags += CONNECT_UPDATE_PROFILE; }

			int w32Result = WNetCancelConnection2W(driveOrShareName, iFlags, force ? 1 : 0);
			if (w32Result != 0)
			{
				throw new System.ComponentModel.Win32Exception(w32Result);
			}
		}

		#endregion

	}

}
