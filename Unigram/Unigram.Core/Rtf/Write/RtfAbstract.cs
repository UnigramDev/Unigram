using System;

namespace Unigram.Core.Rtf.Write
{
    /// <summary>
    /// Internal use only.
    /// Objects that are renderable can emit RTF code.
    /// </summary>
    abstract public class RtfRenderable
    {
        /// <summary>
        /// Internal use only.
        /// Emit RTF code.
        /// </summary>
        /// <returns>RTF code</returns>
        abstract public string Render();
    }

    /// <summary>
    /// Internal use only.
    /// RtfBlock is a content block that cannot contain other blocks.
    /// For example, an image is an RtfBlock because it cannot contain
    /// other content block such as another image, a paragraph, a table,
    /// etc.
    /// </summary>
    abstract public class RtfBlock : RtfRenderable
    {
        /// <summary>
        /// How this block is aligned in its containing block.
        /// </summary>
        abstract public Align Alignment { get; set; }
        /// <summary>
        /// By what distance this block is separated from others in
        /// the containing block.
        /// </summary>
        abstract public Margins Margins { get; }
        /// <summary>
        /// Default character formats.
        /// </summary>
        abstract public RtfCharFormat DefaultCharFormat { get; }
        /// <summary>
        /// When set to true, this block will be arranged in the beginning
        /// of a new page.
        /// </summary>
        abstract public bool StartNewPage { get; set; }
        /// <summary>
        /// Internal use only.
        /// Beginning RTF control words for this block.
        /// </summary>
        abstract internal string BlockHead { set; }
        /// <summary>
        /// Internal use only.
        /// Ending RTF control word for this block.
        /// </summary>
        abstract internal string BlockTail { set; }

        protected string AlignmentCode()
        {
            switch (Alignment)
            {
                case Align.Left:
                    return @"\ql";
                case Align.Right:
                    return @"\qr";
                case Align.Center:
                    return @"\qc";
                case Align.FullyJustify:
                    return @"\qj";
                default:
                    return @"\qd";
            }
        }
    }
}
