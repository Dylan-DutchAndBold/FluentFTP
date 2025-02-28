﻿using System;
using System.Globalization;

#if NET45
using System.Threading.Tasks;

#endif

namespace FluentFTP.Helpers.Parsers
{
	internal static class FtpIBMzOSParser
	{
		/// <summary>
		/// Checks if the given listing is a valid IBM z/OS file listing
		/// </summary>
		public static bool IsValid(FtpClient client, string[] listing)
		{
			// Check validity by using the title line
			// USS Realm     : "total nnnn"
			// Dataset       : "Volume Unit    Referred Ext Used Recfm Lrecl BlkSz Dsorg Dsname"
			// Member        : " Name     VV.MM   Created       Changed      Size  Init   Mod   Id"
			// Member Loadlib: " Name      Size     TTR   Alias-of AC--------- Attributes--------- Amode Rmode"

			return listing[0].Contains("total") ||
		           listing[0].Contains("Volume Unit") ||
				   listing[0].Contains("Name     VV.MM") ||
				   listing[0].Contains("Name      Size     TTR");
		}

		/// <summary>
		/// Parses IBM z/OS format listings
		/// </summary>
		/// <param name="client">The FTP client</param>
		/// <param name="record">A line from the listing</param>
		/// <returns>FtpListItem if the item is able to be parsed</returns>
		public static FtpListItem Parse(FtpClient client, string record)
		{
			// Skip title line - all modes have one. 
			// Also set zOSListingRealm to remember the mode we are in

			// "total nnnn"
			if (record.Contains("total"))
			{
				client.zOSListingRealm = FtpzOSListRealm.Unix;
				return null;
			}

			// "Volume Unit    Referred Ext Used Recfm Lrecl BlkSz Dsorg Dsname"
			if (record.Contains("Volume Unit"))
			{
				client.zOSListingRealm = FtpzOSListRealm.Dataset;
				return null;
			}

			// " Name     VV.MM   Created       Changed      Size  Init   Mod   Id"
			if (record.Contains("Name     VV.MM"))
			{
				client.zOSListingRealm = FtpzOSListRealm.Member;
				return null;
			}

			// "Name      Size     TTR   Alias-of AC--------- Attributes--------- Amode Rmode"
			if (record.Contains("Name      Size     TTR"))
			{
				client.zOSListingRealm = FtpzOSListRealm.MemberU;
				return null;
			}

			if (client.zOSListingRealm == FtpzOSListRealm.Unix)
			{
				// unix mode
				//
				//total 320
				//
				return FtpUnixParser.Parse(client, record);
			}

			if (client.zOSListingRealm == FtpzOSListRealm.Dataset)
			{
				// PS/PO mode
				//
				//Volume Unit    Referred Ext Used Recfm Lrecl BlkSz Dsorg Dsname    
				//ANSYBG 3390   2020/01/03  1   15  VB   32756 32760  PS  $.ADATA.XAA
				//

				// Ignore title line AND also ignore "VSAM", "Not Mounted" and "Error determining attributes"

				if (record.Substring(51, 4).Trim() == "PO" || record.Substring(51, 4).Trim() == "PS")
				{
					string volume = record.Substring(0, 6);
					string unit = record.Substring(7, 4);
					string referred = record.Substring(14, 10).Trim();
					string ext = record.Substring(25, 2).Trim();
					string used = record.Substring(27, 5).Trim();
					string recfm = record.Substring(34, 4).Trim();
					string lrecl = record.Substring(39, 5).Trim();
					string blksz = record.Substring(45, 5).Trim();
					string dsorg = record.Substring(51, 4).Trim();
					string dsname = record.Remove(0, 56).Trim().Split(' ')[0];
					bool isDir = dsorg == "PO";
					var lastModifiedStr = referred;
					if (lastModifiedStr != "**NONE**")
					{
						lastModifiedStr += " 00:00";
					}
					var lastModified = ParseDateTime(client, lastModifiedStr);
					var size = EstimateSize(used);
					var file = new FtpListItem(record, dsname, size, isDir, ref lastModified);
					return file;
				}
				return null;
			}

			if (client.zOSListingRealm == FtpzOSListRealm.Member)
			{
				// Member mode
				//
				// Name     VV.MM   Created       Changed      Size  Init   Mod   Id   
				//$2CPF1    01.01 2001/10/18 2001/10/18 11:58    29    29     0 QFX3076
				//

				string name = record.Substring(0, 8).Trim();
				string changed = string.Empty;
				string records = "0";
				// Member stats may be empty
				if (record.TrimEnd().Length > 8)
				{
					string vvmm = record.Substring(10, 5).Trim();
					string created = record.Substring(17, 10).Trim();
					changed = record.Substring(27, 16).Trim();
					records = record.Substring(44, 5).Trim();
					string init = record.Substring(50, 5).Trim();
					string mod = record.Substring(56, 5).Trim();
					string id = record.Substring(62, 6).Trim();
				}
				bool isDir = false;
				var lastModifiedStr = changed;
				var lastModified = ParseDateTime(client, lastModifiedStr);
				var size = EstimateSizeMEM(records);
				var file = new FtpListItem(record, name, size, isDir, ref lastModified);
				return file;
			}

			if (client.zOSListingRealm == FtpzOSListRealm.MemberU)
			{
				// Member Loadlib mode
				//
				// Name      Size     TTR   Alias-of AC --------- Attributes --------- Amode Rmode
				//EAGKCPT   000058   000009          00 FO             RN RU            31    ANY
				//EAGRTPRC  005F48   000011 EAGRTALT 00 FO             RN RU            31    ANY
				//

				string name = record.Substring(0, 8).Trim();
				string changed = string.Empty;
				string memsize = record.Substring(10,6);
				string TTR = record.Substring(19, 6);
				string Alias = record.Substring(26, 8).Trim();
				string Attributes = record.Substring(38, 30);
				string Amode = record.Substring(70, 2);
				string Rmode = record.Substring(76, 3);
				bool isDir = false;
				var lastModifiedStr = changed;
				var lastModified = ParseDateTime(client, lastModifiedStr);
				var size = int.Parse(memsize, System.Globalization.NumberStyles.HexNumber);
				var file = new FtpListItem(record, name, size, isDir, ref lastModified);
				return file;
			}

			return null;
		}

		/// <summary>
		/// Parses the last modified date from IBM z/OS format listings
		/// </summary>
		private static DateTime ParseDateTime(FtpClient client, string lastModifiedStr)
		{
			var lastModified = DateTime.MinValue;
			if (lastModifiedStr == string.Empty || lastModifiedStr == "**NONE**")
			{
				return lastModified;
			}
			lastModified = DateTime.ParseExact(lastModifiedStr, @"yyyy'/'MM'/'dd HH':'mm", client.ListingCulture.DateTimeFormat, DateTimeStyles.None);

			return lastModified;
		}

		/// <summary>
		/// This is a upper bound and an estimate of the file size.
		/// </summary>
		private static long EstimateSize(string used)
		{
			int n_used = int.Parse(used);   // # of tracks used

			// Assume 3390 device
			long bytesPerTrack = 56664;

			return n_used * bytesPerTrack;
		}

		/// <summary>
		/// Give the caller the number of records instead of a file size.
		/// </summary>
		private static long EstimateSizeMEM(string records)
		{
			int n_records = int.Parse(records);   // # of records of member

			// We don't know LRECL
			long bytesPerRecord = 1;

			return n_records * bytesPerRecord;
		}
	}
}
