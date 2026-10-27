using System;

namespace ClickOCR
{
    /// <summary>Where the text is taken from, relative to the click point.</summary>
    internal enum GrabMode
    {
        /// <summary>Just the word under the pointer.</summary>
        Word,
        /// <summary>The whole OCR line (the "string") that contains the word.</summary>
        Line,
        /// <summary>Every line in the captured area.</summary>
        Block
    }
}
