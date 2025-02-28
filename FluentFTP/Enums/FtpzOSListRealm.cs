﻿using System;

namespace FluentFTP {
	/// <summary>
	/// Flags that can control how a file listing is performed. If you are unsure what to use, set it to Auto.
	/// </summary>
	[Flags]
	public enum FtpzOSListRealm {
		/// <summary>
		/// HFS / USS 
		/// </summary>
		Unix = 0,

		/// <summary>
		/// z/OS classic dataset
		/// </summary>
		Dataset = 1,

		/// <summary>
		/// Partitioned dataset member, RECFM != U
		/// </summary>
		Member = 2,

		/// <summary>
		/// Partitioned dataset member, RECFM = U
		/// </summary>
		MemberU = 3
	}
}