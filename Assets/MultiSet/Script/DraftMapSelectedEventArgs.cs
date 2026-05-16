using System;

namespace MultiSet
{
	public class DraftMapSelectedEventArgs : EventArgs
	{
		public DraftMap SelectedDraft { get; }

		public string CreationDate { get; }

		public DraftMapSelectedEventArgs(DraftMap selectedDraft, string creationDate = null)
		{
			SelectedDraft = selectedDraft;
			CreationDate = creationDate;
		}
	}
}
