using System;
using System.Collections.Generic;
using System.Text;

namespace Unigram.Core.Rtf.Write
{
    /// <summary>
    /// Summary description for RtfParagraph
    /// </summary>
    public class RtfParagraph : RtfBlock
    {
        private StringBuilder _text;
        private float _linespacing;
        private Margins _margins;
        private Align _align;
        private List<RtfCharFormat> _charFormats;
        protected bool _allowFootnote;
        protected bool _allowControlWord;
        private List<RtfFootnote> _footnotes;
        private List<RtfFieldControlWord> _controlWords;
        private string _blockHead;
        private string _blockTail;
        private bool _startNewPage;
        private float _firstLineIndent;
        private RtfCharFormat _defaultCharFormat;
        
        protected struct Token
        {
            public string text;
            public bool isControl;
        }
        
        private class DisjointRange
        {
            public DisjointRange()
            {
                head = -1;
                tail = -1;
                format = null;
            }
            public int head;
            public int tail;
            public RtfCharFormat format;
        }

        public RtfParagraph()
            : this(false, false)
        {
        }

        public RtfParagraph(bool allowFootnote, bool allowControlWord)
        {
            _text = new StringBuilder();
            _linespacing = -1;
            _margins = new Margins();
            _align = Align.Left; //Changed default to .Left as .None was spreading text accross page.
            _charFormats = new List<RtfCharFormat>();
            _allowFootnote = allowFootnote;
            _allowControlWord = allowControlWord;
            _footnotes = new List<RtfFootnote>();
            _controlWords = new List<RtfFieldControlWord>();
            _blockHead = @"{\pard";
            _blockTail = @"\par}";
            _startNewPage = false;
            _firstLineIndent = 0;
            _defaultCharFormat = null;
        }
        
        public StringBuilder Text
        {
            get
            {
                return _text;
            }
        }
        
        public float LineSpacing
        {
            get
            {
                return _linespacing;
            }
            set
            {
                _linespacing = value;
            }
        }
        
        public float FirstLineIndent
        {
            get
            {
                return _firstLineIndent;
            }
            set
            {
                _firstLineIndent = value;
            }
        }

        public void setText(string text)
        {
            _text = new StringBuilder(text);
        }

        public override RtfCharFormat DefaultCharFormat
        {
            get
            {
                if (_defaultCharFormat == null) {
                    _defaultCharFormat = new RtfCharFormat(-1, -1, _text.Length);
                }
                return _defaultCharFormat;
            }
        }

        public override bool StartNewPage
        {
            get
            {
                return _startNewPage;
            }
            set
            {
                _startNewPage = value;
            }
        }
        
        public override Align Alignment
        {
            get
            {
                return _align;
            }
            set
            {
                _align = value;
            }
        }

        public override Margins Margins
        {
            get
            {
                return _margins;
            }
        }
        
        internal override string BlockHead
        {
            set
            {
                _blockHead = value;
            }
        }

        internal override string BlockTail
        {
            set
            {
                _blockTail = value;
            }
        }

        /// <summary>
        /// Add a character formatting to a range in this paragraph.
        /// To specify the whole paragraph as the range, set begin = end = -1.
        /// Format that is added latter will override the former, if their
        /// range overlays each other.
        /// </summary>
        /// <param name="begin">Beginning of the range</param>
        /// <param name="end">End of the range</param>
        public RtfCharFormat addCharFormat(int begin, int end)
        {
            RtfCharFormat fmt = new RtfCharFormat(begin, end, _text.Length);
            _charFormats.Add(fmt);
            return fmt;
        }
        
        public RtfCharFormat addCharFormat()
        {
            return addCharFormat(-1, -1);
        }
        
        public RtfFootnote addFootnote(int position)
        {
            if (!_allowFootnote) {
                throw new Exception("Footnote is not allowed.");
            }
            RtfFootnote fnt = new RtfFootnote(position, _text.Length);
            _footnotes.Add(fnt);
            return fnt;
        }

        public void addControlWord(int position, RtfFieldControlWord.FieldType type)
        {
            if (!_allowControlWord) {
                throw new Exception("ControlWord is not allowed.");
            }
            RtfFieldControlWord w = new RtfFieldControlWord(position, type);
            for (int i = 0; i < _controlWords.Count; i++) {
                if (_controlWords[i].Position == w.Position) {
                    _controlWords[i] = w;
                    return;
                }
            }
            _controlWords.Add(w);
        }

        protected LinkedList<Token> buildTokenList()
        {
            int count;
            Token token;
            LinkedList<Token> tokList = new LinkedList<Token>();
            LinkedListNode<Token> node;
            List<DisjointRange> dranges = new List<DisjointRange>();

            #region Build head[] and tail[] from char format range for later use.
            // --------------------------------------------------
            // Transform possibly overlapped character format ranges into
            // disjoint ranges.
            // --------------------------------------------------
            for (int i = 0; i < _charFormats.Count; i++) {
                RtfCharFormat fmt = _charFormats[i];
                DisjointRange range = null;
                if (fmt.Begin == -1 && fmt.End == -1) {
                    range = new DisjointRange();
                    range.head = 0;
                    range.tail = _text.Length - 1;
                    range.format = fmt;
                } else if (fmt.Begin <= fmt.End) {
                    range = new DisjointRange();
                    range.head = fmt.Begin;
                    range.tail = fmt.End;
                    range.format = fmt;
                } else {
                    continue;
                }
                if (range.tail >= _text.Length) {
                    range.tail = _text.Length - 1;
                    if (range.head > range.tail) {
                        continue;
                    }
                }
                // make the ranges disjoint from each other.
                List<DisjointRange> delList = new List<DisjointRange>();
                List<DisjointRange> addList = new List<DisjointRange>();
                List<DisjointRange> addAnchorList = new List<DisjointRange>();
                for (int j = 0; j < dranges.Count; j++) {
                    DisjointRange r = dranges[j];
                    if (range.head <= r.head && range.tail >= r.tail) {
                        // former range is totally covered by the later
                        //       |--------| r
                        //   |-----------------| range
                        delList.Add(r);
                    } else if (range.head <= r.head && range.tail >= r.head && range.tail < r.tail) {
                        // former range is partially covered
                        //          |------------------| r
                        //     |-----------------| range
                        r.head = range.tail + 1;
                    } else if (range.head > r.head && range.head <= r.tail && range.tail >= r.tail) {
                        // former range is partially covered
                        //     |------------------| r
                        //          |-----------------| range
                        r.tail = range.head - 1;
                    } else if (range.head > r.head && range.tail < r.tail) {
                        // later range is totally covered by the former
                        //   |----------------------| r
                        //        |---------| range
                        DisjointRange newRange = new DisjointRange();
                        newRange.head = range.tail + 1;
                        newRange.tail = r.tail;
                        newRange.format = r.format;
                        r.tail = range.head - 1;
                        addList.Add(newRange);
                        addAnchorList.Add(r);
                    }
                }
                dranges.Add(range);
                for (int j = 0; j < delList.Count; j++) {
                    dranges.Remove(delList[j]);
                }
                for (int j = 0; j < addList.Count; j++) {
                    int index = dranges.IndexOf(addAnchorList[j]);
                    if (index < 0) {
                        continue;
                    }
                    dranges.Insert(index, addList[j]);
                }
            }
            #endregion
            token = new Token();
            token.text = _text.ToString();
            token.isControl = false;
            tokList.AddLast(token);
            #region Build token list from head[] and tail[].
            // --------------------------------------------------
            // Build token list from head[] and tail[].
            // --------------------------------------------------
            for (int i = 0; i < dranges.Count; i++) {
                DisjointRange r = dranges[i];
                count = 0;
                // process head[i]
                if (r.head == 0) {
                    Token newTok = new Token();
                    newTok.isControl = true;
                    newTok.text = r.format.renderHead();
                    tokList.AddFirst(newTok);
                } else {
                    node = tokList.First;
                    while (node != null) {
                        Token tok = node.Value;

                        if (!tok.isControl) {
                            count += tok.text.Length;
                            if (count == r.head) {
                                Token newTok = new Token();
                                newTok.isControl = true;
                                newTok.text = r.format.renderHead();
                                while (node.Next != null && node.Next.Value.isControl) {
                                    node = node.Next;
                                }
                                tokList.AddAfter(node, newTok);
                                break;
                            } else if (count > r.head) {
                                LinkedListNode<Token> newNode;
                                Token newTok1 = new Token();
                                newTok1.isControl = false;
                                newTok1.text = tok.text.Substring(0, tok.text.Length - (count - r.head));
                                newNode = tokList.AddAfter(node, newTok1);
                                Token newTok2 = new Token();
                                newTok2.isControl = true;
                                newTok2.text = r.format.renderHead();
                                newNode = tokList.AddAfter(newNode, newTok2);
                                Token newTok3 = new Token();
                                newTok3.isControl = false;
                                newTok3.text = tok.text.Substring(tok.text.Length - (count - r.head));
                                newNode = tokList.AddAfter(newNode, newTok3);
                                tokList.Remove(node);
                                break;
                            }
                        }
                        node = node.Next;
                    }
                }
                // process tail[i]
                count = 0;
                node = tokList.First;
                while (node != null) {
                    Token tok = node.Value;

                    if (!tok.isControl) {
                        count += tok.text.Length;
                        if (count - 1 == r.tail) {
                            Token newTok = new Token();
                            newTok.isControl = true;
                            newTok.text = r.format.renderTail();
                            tokList.AddAfter(node, newTok);
                            break;
                        } else if (count - 1 > r.tail) {
                            LinkedListNode<Token> newNode;
                            Token newTok1 = new Token();
                            newTok1.isControl = false;
                            newTok1.text = tok.text.Substring(0, tok.text.Length - (count - r.tail) + 1);
                            newNode = tokList.AddAfter(node, newTok1);
                            Token newTok2 = new Token();
                            newTok2.isControl = true;
                            newTok2.text = r.format.renderTail();
                            newNode = tokList.AddAfter(newNode, newTok2);
                            Token newTok3 = new Token();
                            newTok3.isControl = false;
                            newTok3.text = tok.text.Substring(tok.text.Length - (count - r.tail) + 1);
                            newNode = tokList.AddAfter(newNode, newTok3);
                            tokList.Remove(node);
                            break;
                        }
                    }
                    node = node.Next;
                }
            } // end for each char format
            #endregion
            #region Insert footnote into token list.
            // --------------------------------------------------
            // Insert footnote into token list.
            // --------------------------------------------------
            for (int i = 0; i < _footnotes.Count; i++) {
                int pos = _footnotes[i].Position;
                if (pos >= _text.Length) {
                    continue;
                }
                
                count = 0;
                node = tokList.First;
                while (node != null) {
                    Token tok = node.Value;
                    
                    if (!tok.isControl) {
                        count += tok.text.Length;
                        if (count - 1 == pos) {
                            Token newTok = new Token();
                            newTok.isControl = true;
                            newTok.text = _footnotes[i].Render();
                            tokList.AddAfter(node, newTok);
                            break;
                        } else if (count - 1 > pos) {
                            LinkedListNode<Token> newNode;
                            Token newTok1 = new Token();
                            newTok1.isControl = false;
                            newTok1.text = tok.text.Substring(0, tok.text.Length - (count - pos) + 1);
                            newNode = tokList.AddAfter(node, newTok1);
                            Token newTok2 = new Token();
                            newTok2.isControl = true;
                            newTok2.text = _footnotes[i].Render();
                            newNode = tokList.AddAfter(newNode, newTok2);
                            Token newTok3 = new Token();
                            newTok3.isControl = false;
                            newTok3.text = tok.text.Substring(tok.text.Length - (count - pos) + 1);
                            newNode = tokList.AddAfter(newNode, newTok3);
                            tokList.Remove(node);
                            break;
                        }
                    }
                    node = node.Next;
                }
            }
            #endregion
            #region Insert control words into token list.
            // --------------------------------------------------
            // Insert control words into token list.
            // --------------------------------------------------
            for (int i = 0; i < _controlWords.Count; i++) {
                int pos = _controlWords[i].Position;
                if (pos >= _text.Length) {
                    continue;
                }

                count = 0;
                node = tokList.First;
                while (node != null) {
                    Token tok = node.Value;

                    if (!tok.isControl) {
                        count += tok.text.Length;
                        if (count - 1 == pos) {
                            Token newTok = new Token();
                            newTok.isControl = true;
                            newTok.text = _controlWords[i].Render();
                            tokList.AddAfter(node, newTok);
                            break;
                        } else if (count - 1 > pos) {
                            LinkedListNode<Token> newNode;
                            Token newTok1 = new Token();
                            newTok1.isControl = false;
                            newTok1.text = tok.text.Substring(0, tok.text.Length - (count - pos) + 1);
                            newNode = tokList.AddAfter(node, newTok1);
                            Token newTok2 = new Token();
                            newTok2.isControl = true;
                            newTok2.text = _controlWords[i].Render();
                            newNode = tokList.AddAfter(newNode, newTok2);
                            Token newTok3 = new Token();
                            newTok3.isControl = false;
                            newTok3.text = tok.text.Substring(tok.text.Length - (count - pos) + 1);
                            newNode = tokList.AddAfter(newNode, newTok3);
                            tokList.Remove(node);
                            break;
                        }
                    }
                    node = node.Next;
                }
            }
            #endregion
            
            return tokList;
        }
        
        protected string extractTokenList(LinkedList<Token> tokList)
        {
            LinkedListNode<Token> node;
            StringBuilder result = new StringBuilder();

            node = tokList.First;
            while (node != null) {
                if (node.Value.isControl) {
                    result.Append(node.Value.text);
                } else {
                    result.Append(RtfUtility.unicodeEncode(node.Value.text));
                }
                node = node.Next;
            }
            return result.ToString();
        }
        
        public override string Render()
        {
            LinkedList<Token> tokList = buildTokenList();
            StringBuilder result = new StringBuilder(_blockHead);

            if (_startNewPage) {
                result.Append(@"\pagebb");
            }
            
            if (_linespacing >= 0) {
                result.Append(@"\sl-" + RtfUtility.pt2Twip(_linespacing) + @"\slmult0");
            }
            if (_margins[Direction.Top] > 0) {
                result.Append(@"\sb" + RtfUtility.pt2Twip(_margins[Direction.Top]));
            }
            if (_margins[Direction.Bottom] > 0) {
                result.Append(@"\sa" + RtfUtility.pt2Twip(_margins[Direction.Bottom]));
            }
            if (_margins[Direction.Left] > 0) {
                result.Append(@"\li" + RtfUtility.pt2Twip(_margins[Direction.Left]));
            }
            if (_margins[Direction.Right] > 0) {
                result.Append(@"\ri" + RtfUtility.pt2Twip(_margins[Direction.Right]));
            }
            //if (_firstLineIndent != 0) {
            result.Append(@"\fi" + RtfUtility.pt2Twip(_firstLineIndent));
            //}
            result.Append(AlignmentCode());
            result.AppendLine();
            
            // insert default char format intto the 1st position of _charFormats
            if (_defaultCharFormat != null) {
                result.AppendLine(_defaultCharFormat.renderHead());
            }
            result.AppendLine(extractTokenList(tokList));
            if (_defaultCharFormat != null) {
                result.Append(_defaultCharFormat.renderTail());
            }
            
            result.AppendLine(_blockTail);
            return result.ToString();
        }
    }
}
