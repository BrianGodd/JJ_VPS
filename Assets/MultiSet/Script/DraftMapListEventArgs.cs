using System;
using System.Collections.Generic;

namespace MultiSet
{
	public class DraftMapListEventArgs : EventArgs
	{
		public List<DraftMap> DraftMaps { get; }

		public bool HasDrafts => DraftMaps != null && DraftMaps.Count > 0;

		public DraftMapListEventArgs(List<DraftMap> draftMaps)
		{
			DraftMaps = draftMaps ?? new List<DraftMap>();
		}
	}
}
