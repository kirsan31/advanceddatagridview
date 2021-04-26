#region License
// Advanced DataGridView
//
// Original work Copyright (c), 2013 Zuby <zuby@me.com> 
// Modified work Copyright (c), 2014 Davide Gironi <davide.gironi@gmail.com>
//
// Please refer to LICENSE file for licensing information.
#endregion

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Zuby.ADGV
{

    [System.ComponentModel.DesignerCategory("")]
    internal class ColumnHeaderCell : DataGridViewColumnHeaderCell
    {

        #region public events

        public event ColumnHeaderCellEventHandler FilterPopup;
        public event ColumnHeaderCellEventHandler SortChanged;
        public event ColumnHeaderCellEventHandler FilterChanged;

        #endregion


        #region class properties

#if NETFRAMEWORK
        private const int _defFltImgSize = 16;
#else
        private const int _defFltImgSize = 18;
#endif
        private Image _filterImage = Properties.Resources.ColumnHeader_UnFiltered;
        private static readonly Size _filterButtonImageSize = new Size(MenuStrip.Scale(_defFltImgSize, MenuStrip.GetScalingFactor()), MenuStrip.Scale(_defFltImgSize, MenuStrip.GetScalingFactor()));
        private bool _filterButtonPressed;
        private bool _filterButtonOver;
        private Rectangle _filterButtonOffsetBounds = Rectangle.Empty;
        private Rectangle _filterButtonImageBounds = Rectangle.Empty;
        private Padding _filterButtonMargin = new Padding(3, 4, 3, 4);
        private bool _filterEnabled;

        private const bool FilterDateAndTimeDefaultEnabled = false;

        #endregion


        #region constructors

        /// <summary>
        /// ColumnHeaderCell constructor
        /// </summary>
        /// <param name="oldCell">Can't be a <see cref="ColumnHeaderCell"/> here.</param>
        /// <param name="filterEnabled"></param>
        public ColumnHeaderCell(DataGridViewColumnHeaderCell oldCell, bool filterEnabled) : base()
        {
            System.Diagnostics.Debug.Assert(oldCell is not ColumnHeaderCell, $"oldCell is {nameof(ColumnHeaderCell)} in {nameof(ColumnHeaderCell)} constructor!");

            // Below, We will lost State and DataGridView.KeyboardToolTip :(
            Tag = oldCell.Tag;
            ErrorText = oldCell.ErrorText;
            ToolTipText = oldCell.ToolTipText;
            Value = oldCell.Value;
            ValueType = oldCell.ValueType;
            // This is bad - we need to clone it in ideal
            ContextMenuStrip = oldCell.ContextMenuStrip;
            oldCell.ContextMenuStrip = null; // clear reference
            if (oldCell.HasStyle)
                Style = new DataGridViewCellStyle(oldCell.Style);

            MenuStrip = new MenuStrip(oldCell.OwningColumn.ValueType);
            MenuStrip.FilterChanged += new EventHandler(MenuStrip_FilterChanged);
            MenuStrip.SortChanged += new EventHandler(MenuStrip_SortChanged);

            _filterEnabled = filterEnabled;
            IsFilterDateAndTimeEnabled = FilterDateAndTimeDefaultEnabled;
            IsSortEnabled = true;
            IsFilterEnabled = true;
            IsFilterChecklistEnabled = true;
        }

        /// <summary>
        /// Used only in <see cref="Clone"/>
        /// </summary>
        public ColumnHeaderCell()
        {
        }

        #endregion

        private Image FilterImage
        {
            get => _filterImage;
            set
            {
                var oldImg = _filterImage;
                _filterImage = value;
                oldImg?.Dispose();
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _filterImage?.Dispose();
                if (MenuStrip != null)
                {
                    MenuStrip.FilterChanged -= MenuStrip_FilterChanged;
                    MenuStrip.SortChanged -= MenuStrip_SortChanged;
                    MenuStrip.Dispose();
                    MenuStrip = null;
                }
            }
            base.Dispose(disposing);
        }

        #region public clone

        /// <summary>
        /// Clone the ColumnHeaderCell
        /// </summary>
        /// <returns></returns>
        public override object Clone()
        {
            var columnHeaderCell = (ColumnHeaderCell)base.Clone();
            if (MenuStrip != null)
            {
                // This is bad - we need to clone it in ideal
                columnHeaderCell.MenuStrip = MenuStrip;
                columnHeaderCell.EventsSubscribe();
                // unsubscribe and clear reference
                MenuStrip.FilterChanged -= MenuStrip_FilterChanged;
                MenuStrip.SortChanged -= MenuStrip_SortChanged;
                MenuStrip = null;
            }
            columnHeaderCell.FilterImage = (Image)FilterImage.Clone();
            columnHeaderCell._filterButtonPressed = _filterButtonPressed;
            columnHeaderCell._filterButtonOver = _filterButtonOver;
            columnHeaderCell._filterButtonOffsetBounds = _filterButtonOffsetBounds;
            columnHeaderCell._filterButtonImageBounds = _filterButtonImageBounds;
            columnHeaderCell._filterButtonMargin = _filterButtonMargin;
            columnHeaderCell._filterEnabled = _filterEnabled;
            return columnHeaderCell;
        }

        #endregion


        #region public methods

        /// <summary>
        /// Get or Set the Filter and Sort enabled status
        /// </summary>
        public bool FilterAndSortEnabled
        {
            get
            {
                return _filterEnabled;
            }
            set
            {
                if (!value)
                {
                    _filterButtonPressed = false;
                    _filterButtonOver = false;
                }

                if (value != _filterEnabled)
                {
                    _filterEnabled = value;
                    bool refreshed = false;
                    if (MenuStrip?.FilterString.Length > 0)
                    {
                        MenuStrip_FilterChanged(this, new EventArgs());
                        refreshed = true;
                    }
                    if (MenuStrip?.SortString.Length > 0)
                    {
                        MenuStrip_SortChanged(this, new EventArgs());
                        refreshed = true;
                    }
                    if (!refreshed)
                        RepaintCell();
                }
            }
        }

        /// <summary>
        /// Set or Unset the Filter and Sort to Loaded mode
        /// </summary>
        /// <param name="enabled"></param>
        public void SetLoadedMode(bool enabled)
        {
            MenuStrip?.SetLoadedMode(enabled);
            RefreshImage();
            RepaintCell();
        }

        /// <summary>
        /// Clean Sort
        /// </summary>
        public void CleanSort()
        {
            if (MenuStrip != null && FilterAndSortEnabled)
            {
                MenuStrip.CleanSort();
                RefreshImage();
                RepaintCell();
            }
        }

        /// <summary>
        /// Clean Filter
        /// </summary>
        public void CleanFilter()
        {
            if (MenuStrip != null && FilterAndSortEnabled)
            {
                MenuStrip.CleanFilter();
                RefreshImage();
                RepaintCell();
            }
        }

        /// <summary>
        /// Sort ASC
        /// </summary>
        public void SortASC()
        {
            if (MenuStrip != null && FilterAndSortEnabled)
            {
                MenuStrip.SortASC();
            }
        }

        /// <summary>
        /// Sort DESC
        /// </summary>
        public void SortDESC()
        {
            if (MenuStrip != null && FilterAndSortEnabled)
            {
                MenuStrip.SortDESC();
            }
        }

        /// <summary>
        /// Get the MenuStrip for this ColumnHeaderCell
        /// </summary>
        public MenuStrip MenuStrip { get; private set; }

        /// <summary>
        /// Get the MenuStrip SortType
        /// </summary>
        public MenuStrip.SortType ActiveSortType
        {
            get
            {
                if (MenuStrip != null && FilterAndSortEnabled)
                    return MenuStrip.ActiveSortType;
                else
                    return MenuStrip.SortType.None;
            }
        }

        /// <summary>
        /// Get the MenuStrip FilterType
        /// </summary>
        public MenuStrip.FilterType ActiveFilterType
        {
            get
            {
                if (MenuStrip != null && FilterAndSortEnabled)
                    return MenuStrip.ActiveFilterType;
                else
                    return MenuStrip.FilterType.None;
            }
        }

        /// <summary>
        /// Get the Sort string
        /// </summary>
        public string SortString
        {
            get
            {
                if (MenuStrip != null && FilterAndSortEnabled)
                    return MenuStrip.SortString;
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Get the Filter string
        /// </summary>
        public string FilterString
        {
            get
            {
                if (MenuStrip != null && FilterAndSortEnabled)
                    return MenuStrip.FilterString;
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Get the Minimum size
        /// </summary>
        public Size MinimumSize
        {
            get
            {
                return new Size(_filterButtonImageSize.Width + _filterButtonMargin.Left + _filterButtonMargin.Right,
                    _filterButtonImageSize.Height + _filterButtonMargin.Bottom + _filterButtonMargin.Top);
            }
        }

        /// <summary>
        /// Get or Set the Sort enabled status
        /// </summary>
        public bool IsSortEnabled
        {
            get
            {
                return MenuStrip?.IsSortEnabled ?? false;
            }
            set
            {
                if(MenuStrip != null)
                    MenuStrip.IsSortEnabled = value;
            }
        }

        /// <summary>
        /// Get or Set the Filter enabled status
        /// </summary>
        public bool IsFilterEnabled
        {
            get
            {
                return MenuStrip?.IsFilterEnabled ?? false;
            }
            set
            {
                if(MenuStrip != null)
                    MenuStrip.IsFilterEnabled = value;
            }
        }

        /// <summary>
        /// Get or Set the Filter enabled status
        /// </summary>
        public bool IsFilterChecklistEnabled
        {
            get
            {
                return MenuStrip?.IsFilterChecklistEnabled ?? false;
            }
            set
            {
                if(MenuStrip != null)
                    MenuStrip.IsFilterChecklistEnabled = value;
            }
        }

        /// <summary>
        /// Get or Set the FilterDateAndTime enabled status
        /// </summary>
        public bool IsFilterDateAndTimeEnabled
        {
            get
            {
                return MenuStrip?.IsFilterDateAndTimeEnabled ?? false;
            }
            set
            {
                if(MenuStrip != null)
                    MenuStrip.IsFilterDateAndTimeEnabled = value;
            }
        }

        /// <summary>
        /// Get or Set the NOT IN logic for Filter
        /// </summary>
        public bool IsMenuStripFilterNOTINLogicEnabled
        {
            get
            {
                return MenuStrip?.IsFilterNOTINLogicEnabled ?? false;
            }
            set
            {
                if (MenuStrip != null)
                    MenuStrip.IsFilterNOTINLogicEnabled = value;
            }
        }

        /// <summary>
        /// Set the text filter search nodes behaviour
        /// </summary>
        public bool DoesTextFilterRemoveNodesOnSearch
        {
            get
            {
                return MenuStrip?.DoesTextFilterRemoveNodesOnSearch ?? false;
            }
            set
            {
                if (MenuStrip != null)
                    MenuStrip.DoesTextFilterRemoveNodesOnSearch = value;
            }
        }

        /// <summary>
        /// Enabled or disable Sort capabilities
        /// </summary>
        /// <param name="enabled"></param>
        public void SetSortEnabled(bool enabled)
        {
            if (MenuStrip != null)
            {
                MenuStrip.IsSortEnabled = enabled;
                MenuStrip.SetSortEnabled(enabled);
            }
        }

        /// <summary>
        /// Enable or disable Filter capabilities
        /// </summary>
        /// <param name="enabled"></param>
        public void SetFilterEnabled(bool enabled)
        {
            if (MenuStrip != null)
            {
                MenuStrip.IsFilterEnabled = enabled;
                MenuStrip.SetFilterEnabled(enabled);
            }
        }

        /// <summary>
        /// Enable or disable Filter checklist capabilities
        /// </summary>
        /// <param name="enabled"></param>
        public void SetFilterChecklistEnabled(bool enabled)
        {
            if (MenuStrip != null)
            {
                MenuStrip.IsFilterChecklistEnabled = enabled;
                MenuStrip.SetFilterChecklistEnabled(enabled);
            }
        }

        /// <summary>
        /// Enable or disable Filter custom capabilities
        /// </summary>
        /// <param name="enabled"></param>
        public void SetFilterCustomEnabled(bool enabled)
        {
            if (MenuStrip != null)
            {
                MenuStrip.IsFilterCustomEnabled = enabled;
                MenuStrip.SetFilterCustomEnabled(enabled);
            }
        }

        /// <summary>
        /// Enable or disable Text filter on checklist remove node mode
        /// </summary>
        /// <param name="enabled"></param>
        public void SetChecklistTextFilterRemoveNodesOnSearchMode(bool enabled)
        {
            if (MenuStrip != null)
            {
                MenuStrip.SetChecklistTextFilterRemoveNodesOnSearchMode(enabled);
            }
        }

        #endregion


        #region menustrip events

        /// <summary>
        /// OnFilterChanged event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuStrip_FilterChanged(object sender, EventArgs e)
        {
            RefreshImage();
            RepaintCell();
            if (FilterAndSortEnabled && FilterChanged != null)
                FilterChanged(this, new ColumnHeaderCellEventArgs(MenuStrip, OwningColumn));
        }

        /// <summary>
        /// OnSortChanged event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuStrip_SortChanged(object sender, EventArgs e)
        {
            RefreshImage();
            RepaintCell();
            if (FilterAndSortEnabled && SortChanged != null)
                SortChanged(this, new ColumnHeaderCellEventArgs(MenuStrip, OwningColumn));
        }

        /// <summary>
        /// Subscribe to <see cref="MenuStrip.FilterChanged"/> and <see cref="MenuStrip.SortChanged"/>
        /// </summary>
        private void EventsSubscribe()
        {
            if (MenuStrip != null)
            {
                MenuStrip.FilterChanged += MenuStrip_FilterChanged;
                MenuStrip.SortChanged += MenuStrip_SortChanged;
            }
        }

        #endregion


        #region paint methods

        /// <summary>
        /// Repaint the Cell
        /// </summary>
        private void RepaintCell()
        {
            if (Displayed && DataGridView != null)
                DataGridView.InvalidateCell(this);
        }

        /// <summary>
        /// Refresh the Cell image
        /// </summary>
        private void RefreshImage()
        {
            if (ActiveFilterType == MenuStrip.FilterType.Loaded)
            {
                FilterImage = Properties.Resources.ColumnHeader_SavedFilters;
            }
            else
            {
                if (ActiveFilterType == MenuStrip.FilterType.None)
                {
                    if (ActiveSortType == MenuStrip.SortType.None)
                        FilterImage = Properties.Resources.ColumnHeader_UnFiltered;
                    else if (ActiveSortType == MenuStrip.SortType.ASC)
                        FilterImage = Properties.Resources.ColumnHeader_OrderedASC;
                    else
                        FilterImage = Properties.Resources.ColumnHeader_OrderedDESC;
                }
                else
                {
                    if (ActiveSortType == MenuStrip.SortType.None)
                        FilterImage = Properties.Resources.ColumnHeader_Filtered;
                    else if (ActiveSortType == MenuStrip.SortType.ASC)
                        FilterImage = Properties.Resources.ColumnHeader_FilteredAndOrderedASC;
                    else
                        FilterImage = Properties.Resources.ColumnHeader_FilteredAndOrderedDESC;
                }
            }
        }

        /// <summary>
        /// Pain method
        /// </summary>
        /// <param name="graphics"></param>
        /// <param name="clipBounds"></param>
        /// <param name="cellBounds"></param>
        /// <param name="rowIndex"></param>
        /// <param name="cellState"></param>
        /// <param name="value"></param>
        /// <param name="formattedValue"></param>
        /// <param name="errorText"></param>
        /// <param name="cellStyle"></param>
        /// <param name="advancedBorderStyle"></param>
        /// <param name="paintParts"></param>
        protected override void Paint(
            Graphics graphics,
            Rectangle clipBounds,
            Rectangle cellBounds,
            int rowIndex,
            DataGridViewElementStates cellState,
            object value,
            object formattedValue,
            string errorText,
            DataGridViewCellStyle cellStyle,
            DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            if (SortGlyphDirection != SortOrder.None)
                SortGlyphDirection = SortOrder.None;

            base.Paint(graphics, clipBounds, cellBounds, rowIndex,
                cellState, value, formattedValue,
                errorText, cellStyle, advancedBorderStyle, paintParts);

            // Don't display a dropdown for Image columns
            if (this.OwningColumn.ValueType == typeof(System.Drawing.Bitmap))
                return;

            if (FilterAndSortEnabled && paintParts.HasFlag(DataGridViewPaintParts.ContentBackground))
            {
                _filterButtonOffsetBounds = GetFilterBounds(true);
                _filterButtonImageBounds = GetFilterBounds(false);
                Rectangle buttonBounds = _filterButtonOffsetBounds;
                if (clipBounds.IntersectsWith(buttonBounds))
                {
                    ControlPaint.DrawBorder(graphics, buttonBounds, Color.Gray, ButtonBorderStyle.Solid);
                    buttonBounds.Inflate(-1, -1);
                    using (Brush b = new SolidBrush(_filterButtonOver ? Color.WhiteSmoke : Color.White))
                        graphics.FillRectangle(b, buttonBounds);
                    graphics.DrawImage(FilterImage, buttonBounds);
                }
            }
        }

        /// <summary>
        /// Get the ColumnHeaderCell Bounds
        /// </summary>
        /// <param name="withOffset"></param>
        /// <returns></returns>
        private Rectangle GetFilterBounds(bool withOffset = true)
        {
            Rectangle cell = DataGridView.GetCellDisplayRectangle(ColumnIndex, -1, false);

            Point p = new Point(
                (withOffset ? cell.Right : cell.Width) - _filterButtonImageSize.Width - _filterButtonMargin.Right,
                (withOffset ? cell.Bottom : cell.Height) - _filterButtonImageSize.Height - _filterButtonMargin.Bottom);

            return new Rectangle(p, _filterButtonImageSize);
        }

        #endregion


        #region mouse events

        /// <summary>
        /// OnMouseMove event
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseMove(DataGridViewCellMouseEventArgs e)
        {
            if (FilterAndSortEnabled)
            {
                if (_filterButtonImageBounds.Contains(e.X, e.Y) && !_filterButtonOver)
                {
                    _filterButtonOver = true;
                    RepaintCell();
                }
                else if (!_filterButtonImageBounds.Contains(e.X, e.Y) && _filterButtonOver)
                {
                    _filterButtonOver = false;
                    RepaintCell();
                }
            }
            base.OnMouseMove(e);
        }

        /// <summary>
        /// OnMouseDown event
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseDown(DataGridViewCellMouseEventArgs e)
        {
            if (FilterAndSortEnabled && _filterButtonImageBounds.Contains(e.X, e.Y))
            {
                if (e.Button == MouseButtons.Left && !_filterButtonPressed)
                {
                    _filterButtonPressed = true;
                    _filterButtonOver = true;
                    RepaintCell();
                }
            }
            else
                base.OnMouseDown(e);
        }

        /// <summary>
        /// OnMouseUp event
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseUp(DataGridViewCellMouseEventArgs e)
        {
            if (FilterAndSortEnabled && e.Button == MouseButtons.Left && _filterButtonPressed)
            {
                _filterButtonPressed = false;
                _filterButtonOver = false;
                RepaintCell();
                if (_filterButtonImageBounds.Contains(e.X, e.Y) && FilterPopup != null)
                {
                    FilterPopup(this, new ColumnHeaderCellEventArgs(MenuStrip, OwningColumn));
                }
            }
            base.OnMouseUp(e);
        }

        /// <summary>
        /// OnMouseLeave event
        /// </summary>
        /// <param name="rowIndex"></param>
        protected override void OnMouseLeave(int rowIndex)
        {
            if (FilterAndSortEnabled && _filterButtonOver)
            {
                _filterButtonOver = false;
                RepaintCell();
            }

            base.OnMouseLeave(rowIndex);
        }

        #endregion
    }

    internal delegate void ColumnHeaderCellEventHandler(object sender, ColumnHeaderCellEventArgs e);
    internal class ColumnHeaderCellEventArgs : EventArgs
    {
        public MenuStrip FilterMenu { get; private set; }

        public DataGridViewColumn Column { get; private set; }

        public ColumnHeaderCellEventArgs(MenuStrip filterMenu, DataGridViewColumn column)
        {
            FilterMenu = filterMenu;
            Column = column;
        }
    }

}