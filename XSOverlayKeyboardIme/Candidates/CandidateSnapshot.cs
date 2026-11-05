using System.Text;

namespace XSOverlayKeyboardIme.Candidates
{
    /// <summary>One page of candidates as the IME reported them.</summary>
    internal sealed class CandidateSnapshot
    {
        internal static readonly CandidateSnapshot Empty = new CandidateSnapshot(0, -1, new string[0]);

        internal CandidateSnapshot(uint count, int selected, string[] items)
        {
            Count = count;
            Selected = selected;
            Items = items;
        }

        internal uint Count { get; }

        internal int Selected { get; }

        internal string[] Items { get; }

        internal string Describe()
        {
            var text = new StringBuilder();
            text.Append("count=").Append(Count).Append(" sel=").Append(Selected);
            for (int index = 0; index < Items.Length; index++)
            {
                text.Append(' ').Append('[').Append(index).Append(']').Append(Items[index]);
                if (index == Selected)
                {
                    text.Append('*');
                }
            }

            return text.ToString();
        }
    }
}
