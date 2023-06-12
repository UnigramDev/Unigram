//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UnigramBridge
{
    public class CustomContextMenu : ContextMenuStrip
    {
        //Fields
        private bool isMainMenu;
        private int menuItemHeight = 20;
        private int menuItemWidth = 20;
        private Color menuItemTextColor = Color.Empty;
        private Color primaryColor = Color.Empty;
        private Color MouseOverColor = Color.Empty;
        private Color MouseOverBorderColor = Color.Empty;
        private WindowsTheme systrayTheme = WindowsTheme.Light;
        private Bitmap menuItemHeaderSize;

        //Constructor
        public CustomContextMenu()
        {

        }

        //Properties
        [Browsable(false)]
        public bool IsMainMenu
        {
            get { return isMainMenu; }
            set { isMainMenu = value; }
        }

        [Browsable(false)]
        public int MenuItemHeight
        {
            get { return menuItemHeight; }
            set { menuItemHeight = value; }
        }

        [Browsable(false)]
        public int MenuItemWidth
        {
            get { return menuItemWidth; }
            set { menuItemWidth = value; }
        }

        [Browsable(false)]
        public Color MenuItemTextColor
        {
            get { return menuItemTextColor; }
            set { menuItemTextColor = value; }
        }

        [Browsable(false)]
        public Color PrimaryColor
        {
            get { return primaryColor; }
            set { primaryColor = value; }
        }

        [Browsable(false)]
        public Color MenuItemMouseOverColor
        {
            get { return MouseOverColor; }
            set { MouseOverColor = value; }
        }

        [Browsable(false)]
        public Color MenuItemMouseOverBorderColor
        {
            get { return MouseOverBorderColor; }
            set { MouseOverBorderColor = value; }
        }

        [Browsable(false)]
        public WindowsTheme SystrayTheme
        {
            get { return systrayTheme; }
            set { systrayTheme = value; }
        }

        //Private methods
        private void LoadMenuItemHeight()
        {
            if (isMainMenu)
                menuItemHeaderSize = new Bitmap(menuItemWidth, menuItemHeight);
            else menuItemHeaderSize = new Bitmap(menuItemWidth - 5, menuItemHeight);

            foreach (ToolStripMenuItem menuItemL1 in this.Items)
            {
                menuItemL1.ImageScaling = ToolStripItemImageScaling.None;
                if (menuItemL1.Image == null) menuItemL1.Image = menuItemHeaderSize;

                foreach (ToolStripMenuItem menuItemL2 in menuItemL1.DropDownItems)
                {
                    menuItemL2.ImageScaling = ToolStripItemImageScaling.None;
                    if (menuItemL2.Image == null) menuItemL2.Image = menuItemHeaderSize;

                    foreach (ToolStripMenuItem menuItemL3 in menuItemL2.DropDownItems)
                    {
                        menuItemL3.ImageScaling = ToolStripItemImageScaling.None;
                        if (menuItemL3.Image == null) menuItemL3.Image = menuItemHeaderSize;

                        foreach (ToolStripMenuItem menuItemL4 in menuItemL3.DropDownItems)
                        {
                            menuItemL4.ImageScaling = ToolStripItemImageScaling.None;
                            if (menuItemL4.Image == null) menuItemL4.Image = menuItemHeaderSize;
                            ///Level 5++
                        }
                    }
                }
            }
        }

        //Overrides
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (this.DesignMode == false)
            {
                switch (SystrayTheme)
                {
                    case WindowsTheme.Light:
                        {
                            menuItemTextColor = Color.Black;
                        }
                        break;
                    case WindowsTheme.Dark:
                        {
                            menuItemTextColor = Color.White;
                        }
                        break;
                }

                this.Renderer = new MenuRenderer(isMainMenu, primaryColor, menuItemTextColor, MouseOverColor, MouseOverBorderColor, SystrayTheme);
                LoadMenuItemHeight();
            }
        }

    }

    public class MenuRenderer : ToolStripProfessionalRenderer
    {
        //Fields
        private Color primaryColor;
        private Color textColor;
        private int arrowThickness;
        private WindowsTheme systrayTheme;

        [Browsable(false)]
        public WindowsTheme SystrayTheme
        {
            get { return systrayTheme; }
            set { systrayTheme = value; }
        }

        //Constructor
        public MenuRenderer(bool isMainMenu, Color primaryColor, Color textColor, Color menuItemMouseOverColor, Color menuItemMouseOverBorderColor, WindowsTheme theme)
            : base(new MenuColorTable(isMainMenu, primaryColor, menuItemMouseOverColor, menuItemMouseOverBorderColor, theme))
        {
            RoundedEdges = true;

            this.primaryColor = primaryColor;
            this.systrayTheme = theme;

            if (isMainMenu)
            {
                arrowThickness = 2;
                if (textColor == Color.Empty) //Set Default Color
                    this.textColor = Color.Gainsboro;
                else//Set custom text color 
                    this.textColor = textColor;
            }
            else
            {
                arrowThickness = 1;
                if (textColor == Color.Empty) //Set Default Color
                    this.textColor = Color.DimGray;
                else//Set custom text color
                    this.textColor = textColor;
            }
        }

        //Overrides
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            base.OnRenderItemText(e);
            e.Item.ForeColor = e.Item.Selected ? Color.White : textColor;
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            //Fields
            var graph = e.Graphics;
            var arrowSize = new Size(5, 10);
            var arrowColor = e.Item.Selected ? Color.White : primaryColor;
            var rect = new Rectangle(e.ArrowRectangle.Location.X, (e.ArrowRectangle.Height - arrowSize.Height) / 2,
                arrowSize.Width, arrowSize.Height);
            using (GraphicsPath path = new GraphicsPath())
            using (Pen pen = new Pen(arrowColor, arrowThickness))
            {
                //Drawing
                graph.SmoothingMode = SmoothingMode.AntiAlias;
                path.AddLine(rect.Left, rect.Top, rect.Right, rect.Top + rect.Height / 2);
                path.AddLine(rect.Right, rect.Top + rect.Height / 2, rect.Left, rect.Top + rect.Height);
                graph.DrawPath(pen, path);
            }
        }

        public GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // top left arc  
            path.AddArc(arc, 180, 90);

            // top right arc  
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right arc  
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left arc 
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }

    public class MenuColorTable : ProfessionalColorTable
    {
        //Fields
        private Color backColor;
        private Color leftColumnColor;
        private Color borderColor;
        private Color menuItemBorderColor;
        private Color menuItemSelectedColor;
        private WindowsTheme systrayTheme;

        [Browsable(false)]
        public WindowsTheme SystrayTheme
        {
            get { return systrayTheme; }
            set { systrayTheme = value; }
        }

        //Constructor
        public MenuColorTable(bool isMainMenu, Color primaryColor, Color menuItemSelectedColor, Color menuItemBorderColor, WindowsTheme theme) : base()
        {
            this.UseSystemColors = false;
            this.systrayTheme = theme;

            if (menuItemSelectedColor == Color.Empty)
            {
                menuItemSelectedColor = Color.FromArgb(51, 102, 255);
            }

            if (menuItemBorderColor == Color.Empty)
            {
                menuItemBorderColor = Color.FromArgb(25, 51, 127);
            }

            if (isMainMenu)
            {
                switch (SystrayTheme)
                {
                    case WindowsTheme.Light:
                        {
                            backColor = Color.FromArgb(255, 255, 255);
                            leftColumnColor = Color.FromArgb(242, 242, 242);
                            borderColor = Color.FromArgb(193, 193, 193);
                            this.menuItemBorderColor = menuItemBorderColor;
                            this.menuItemSelectedColor = menuItemSelectedColor;
                        }
                        break;
                    case WindowsTheme.Dark:
                        {
                            backColor = Color.FromArgb(37, 39, 60);
                            leftColumnColor = Color.FromArgb(32, 33, 51);
                            borderColor = Color.FromArgb(32, 33, 51);
                            this.menuItemBorderColor = menuItemBorderColor;
                            this.menuItemSelectedColor = menuItemSelectedColor;
                        }
                        break;
                }
            }
            else
            {
                backColor = Color.White;
                leftColumnColor = Color.LightGray;
                borderColor = Color.LightGray;
                this.menuItemBorderColor = menuItemBorderColor;
                this.menuItemSelectedColor = menuItemSelectedColor;
            }
        }

        //Overrides
        public override Color ToolStripDropDownBackground { get { return backColor; } }
        public override Color MenuBorder { get { return borderColor; } }
        public override Color MenuItemBorder { get { return menuItemBorderColor; } }
        public override Color MenuItemSelected { get { return menuItemSelectedColor; } }

        public override Color ImageMarginGradientBegin { get { return leftColumnColor; } }
        public override Color ImageMarginGradientMiddle { get { return leftColumnColor; } }
        public override Color ImageMarginGradientEnd { get { return leftColumnColor; } }

        public override Color ButtonSelectedHighlight { get { return menuItemSelectedColor; } }
        public override Color ButtonSelectedHighlightBorder { get { return menuItemBorderColor; } }
    }

    public enum WindowsTheme
    {
        Default = 0,
        Light = 1,
        Dark = 2,
    }
}
