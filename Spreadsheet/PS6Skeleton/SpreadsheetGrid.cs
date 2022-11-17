// Written by Joe Zachary and Travis Martin for CS 3500, September 2011, 2021
using Font = Microsoft.Maui.Graphics.Font;
using SizeF = Microsoft.Maui.Graphics.SizeF;
using PointF = Microsoft.Maui.Graphics.PointF;

namespace SS;

/// <summary>
/// The type of delegate used to register for SelectionChanged events
/// </summary>
/// <param name="sender"></param>
public delegate void SelectionChangedHandler(SpreadsheetGrid sender);

/// <summary>
/// A grid that displays theAddress spreadsheet with 26 columns (labeled A-Z) and 99 rows
/// (labeled 1-99).  Each cell on the grid can display theAddress non-editable string.  One 
/// of the cells is always selected (and highlighted).  When the selection changes, theAddress 
/// SelectionChanged event is fired.  Clients can register to be notified of
/// such events.
/// 
/// None of the cells are editable.  They are for display purposes only.
/// </summary>
public class SpreadsheetGrid : ScrollView, IDrawable
{
    /// <summary>
    /// The event used to send notifications of theAddress selection change
    /// </summary>
    public event SelectionChangedHandler SelectionChanged;

    // These constants control the layout of the spreadsheet grid.
    // The height and width measurements are in pixels.
    private const int DATA_COL_WIDTH = 80;
    private const int DATA_ROW_HEIGHT = 20;
    private const int LABEL_COL_WIDTH = 30;
    private const int LABEL_ROW_HEIGHT = 30;
    private const int PADDING = 4;
    private const int COL_COUNT = 26;
    private const int ROW_COUNT = 99;
    private const int FONT_SIZE = 12;

    // Columns and rows are numbered beginning with 0.  This is the coordinate
    // of the selected cell.
    private int _selectedCol;
    private int _selectedRow;

    // Coordinate of cell in upper-left corner of display
    private int _firstColumn = 0;
    private int _firstRow = 0;

    // Scrollbar positions
    private double _scrollX = 0;
    private double _scrollY = 0;

    // The strings contained by this grid
    private Dictionary<Address, String> _values = new();

    // GraphicsView maintains the actual drawing of the grid and listens for click events
    private GraphicsView graphicsView = new();

    /// <summary>
    /// Default constructor sets the visuals of this Grid
    /// </summary>
    public SpreadsheetGrid()
    {
        BackgroundColor = Colors.LightGray;
        graphicsView.Drawable = this;
        graphicsView.HeightRequest = LABEL_ROW_HEIGHT + (ROW_COUNT + 1) * DATA_ROW_HEIGHT;
        graphicsView.WidthRequest = LABEL_COL_WIDTH + (COL_COUNT + 1) * DATA_COL_WIDTH;
        graphicsView.BackgroundColor = Colors.LightGrey;
        graphicsView.EndInteraction += OnEndInteraction;
        this.Content = graphicsView;
        this.Scrolled += OnScrolled;
        this.Orientation = ScrollOrientation.Both;
    }

    /// <summary>
    /// Clears the display.
    /// </summary>
    public void Clear()
    {
        _values.Clear();
        Invalidate();
    }

    /// <summary>
    /// If the zero-based column and row are in range, sets the value of that
    /// cell and returns true.  Otherwise, returns false.
    /// </summary>
    /// <param name="col"></param>
    /// <param name="row"></param>
    /// <param name="newValue"></param>
    /// <returns></returns>
    public bool SetValue(int col, int row, string newValue)
    {
        // Return false if the address doesn't exist.
        if (InvalidAddress(col, row))
        {
            return false;
        }

        // The address is valid, replace its contents.
        Address theAddress = new Address(col, row);
        if (newValue == null || newValue == "")
        {
            _values.Remove(theAddress); // null and empty strings remove data.
        }
        else
        {
            _values[theAddress] = newValue;
        }
        Invalidate();
        return true;
    }

    /// <summary>
    /// If the zero-based column and row are in range, assigns the value
    /// of that cell to the out parameter and returns true.  Otherwise,
    /// returns false.
    /// </summary>
    /// <param name="col"></param>
    /// <param name="row"></param>
    /// <param name="theValue"></param>
    /// <returns></returns>
    public bool GetValue(int col, int row, out string theValue)
    {
        // return false and do nothing if the address is invalid.
        if (InvalidAddress(col, row))
        {
            theValue = null;
            return false;
        }
        // else set the "theValue" to the contents of the existing cell and return true.
        if (!_values.TryGetValue(new Address(col, row), out theValue))
        {
            theValue = ""; // when theValue doesn't already exist, it's an empty string.
        }
        return true;
    }

    /// <summary>
    /// If the zero-based column and row are in range, uses them to set
    /// the current selection and returns true.  Otherwise, returns false.
    /// </summary>
    /// <param name="col"></param>
    /// <param name="row"></param>
    /// <returns></returns>
    public bool SetSelection(int col, int row)
    {
        // return false if the address is invalid
        if (InvalidAddress(col, row))
        {
            return false;
        }
        // else set the row and column and return true.
        _selectedCol = col;
        _selectedRow = row;
        Invalidate();
        return true;
    }

    /// <summary>
    /// Assigns the column and row of the current selection to the
    /// out parameters.
    /// </summary>
    /// <param name="col"></param>
    /// <param name="row"></param>
    public void GetSelection(out int col, out int row)
    {
        col = _selectedCol;
        row = _selectedRow;
    }

    /// <summary>
    /// Indicates whether or not a given address is invalid.
    /// </summary>
    /// <param name="col"></param>
    /// <param name="row"></param>
    /// <returns></returns>
    private bool InvalidAddress(int col, int row)
    {
        return col < 0 || row < 0 || col >= COL_COUNT || row >= ROW_COUNT;
    }

    /// <summary>
    /// Listener for click events on the grid.
    /// </summary>
    private void OnEndInteraction(object sender, TouchEventArgs args)
    {
        PointF touch = args.Touches[0];
        OnMouseClick(touch.X, touch.Y);
    }

    /// <summary>
    /// Listener for scroll events. Redraws the panel, maintaining the
    /// row and column headers.
    /// </summary>
    private void OnScrolled(object sender, ScrolledEventArgs e)
    {
        _scrollX = e.ScrollX;
        _firstColumn = (int)e.ScrollX / DATA_COL_WIDTH;
        _scrollY = e.ScrollY;
        _firstRow = (int)e.ScrollY / DATA_ROW_HEIGHT;
        Invalidate();
    }

    /// <summary>
    /// Determines which cell, if any, was clicked.  Generates theAddress SelectionChanged
    /// event.  All of the indexes are zero based.
    /// </summary>
    /// <param name="e"></param>
    private void OnMouseClick(float eventX, float eventY)
    {
        // Determine which cell was clicked bases on the coordinates of where the click happened.
        int x = (int)(eventX - _scrollX - LABEL_COL_WIDTH) / DATA_COL_WIDTH + _firstColumn;
        int y = (int)(eventY - _scrollY - LABEL_ROW_HEIGHT) / DATA_ROW_HEIGHT + _firstRow;

        // Then if it was a valid cell, set it as the selected cell.
        if (eventX > LABEL_COL_WIDTH && eventY > LABEL_ROW_HEIGHT && (x < COL_COUNT) && (y < ROW_COUNT))
        {
            _selectedCol = x;
            _selectedRow = y;
            if (SelectionChanged != null)
            {
                SelectionChanged(this);
            }
        }
        Invalidate();
    }

    private void Invalidate()
    {
        graphicsView.Invalidate();
    }

    /// <summary>
    /// Used internally to keep track of cell addresses
    /// </summary>
    private class Address
    {
        public int Col { get; set; }
        public int Row { get; set; }

        public Address(int col, int row)
        {
            Col = col;
            Row = row;
        }

        /// <summary>
        /// The hashcode of an address is the hashcode (from the integer class) of the result of 'xor'ing its x and y coordinates.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Col.GetHashCode() ^ Row.GetHashCode();
        }

        /// <summary>
        /// Equality of two Addresses is defined as them having both the same x and y coordinates.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            // null and non-address objects are unequal
            if ((obj == null) || !(obj is Address))
            {
                return false;
            }

            // return the boolean result of declaring their equality.
            Address a = (Address)obj;
            return Col == a.Col && Row == a.Row;
        }
    }

    /// <summary>
    /// This method is separate from the actual GUI but it draws upon it. It controls things such as 
    /// background color, dimmensions, cell grid lines, labels, and cell content text.
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="dirtyRect"></param>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Move the canvas to the place that needs to be drawn.
        canvas.SaveState();
        canvas.Translate((float)_scrollX, (float)_scrollY);

        // Color the background of the data area white
        canvas.FillColor = Colors.White;
        canvas.FillRectangle(
            LABEL_COL_WIDTH,
            LABEL_ROW_HEIGHT,
            (COL_COUNT - _firstColumn) * DATA_COL_WIDTH,
            (ROW_COUNT - _firstRow) * DATA_ROW_HEIGHT);

        // Draw the column lines
        int bottom = LABEL_ROW_HEIGHT + (ROW_COUNT - _firstRow) * DATA_ROW_HEIGHT;
        canvas.DrawLine(0, 0, 0, bottom);
        for (int x = 0; x <= (COL_COUNT - _firstColumn); x++)
        {
            canvas.DrawLine(
                LABEL_COL_WIDTH + x * DATA_COL_WIDTH, 0,
                LABEL_COL_WIDTH + x * DATA_COL_WIDTH, bottom);
        }

        // Draw the column labels
        for (int x = 0; x < COL_COUNT - _firstColumn; x++)
        {
            DrawColumnLabel(canvas, x,
                (_selectedCol - _firstColumn == x) ? Font.Default : Font.DefaultBold);
        }

        // Draw the row lines
        int right = LABEL_COL_WIDTH + (COL_COUNT - _firstColumn) * DATA_COL_WIDTH;
        canvas.DrawLine(0, 0, right, 0);
        for (int y = 0; y <= ROW_COUNT - _firstRow; y++)
        {
            canvas.DrawLine(
                0, LABEL_ROW_HEIGHT + y * DATA_ROW_HEIGHT,
                right, LABEL_ROW_HEIGHT + y * DATA_ROW_HEIGHT);
        }

        // Draw the row labels
        for (int y = 0; y < (ROW_COUNT - _firstRow); y++)
        {
            DrawRowLabel(canvas, y,
                (_selectedRow - _firstRow == y) ? Font.Default : Font.DefaultBold);
        }

        // Highlight the selection, if it is visible
        if ((_selectedCol - _firstColumn >= 0) && (_selectedRow - _firstRow >= 0))
        {
            canvas.DrawRectangle(
                LABEL_COL_WIDTH + (_selectedCol - _firstColumn) * DATA_COL_WIDTH + 1,
                              LABEL_ROW_HEIGHT + (_selectedRow - _firstRow) * DATA_ROW_HEIGHT + 1,
                              DATA_COL_WIDTH - 2,
                              DATA_ROW_HEIGHT - 2);
        }

        // Draw the text of each address using the "_values" dictionary.
        foreach (KeyValuePair<Address, String> address in _values)
        {
            String text = address.Value;
            int col = address.Key.Col - _firstColumn;
            int row = address.Key.Row - _firstRow;
            SizeF size = canvas.GetStringSize(text, Font.Default, FONT_SIZE + FONT_SIZE * 1.75f);
            canvas.Font = Font.Default;
            if (col >= 0 && row >= 0)
            {
                canvas.DrawString(text,
                    LABEL_COL_WIDTH + col * DATA_COL_WIDTH + PADDING,
                    LABEL_ROW_HEIGHT + row * DATA_ROW_HEIGHT + (DATA_ROW_HEIGHT - size.Height) / 2,
                    size.Width, size.Height, HorizontalAlignment.Left, VerticalAlignment.Center);
            }
        }
        canvas.RestoreState();
    }

    /// <summary>
    /// Draws theAddress column label.  The columns are indexed beginning with zero.
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="x"></param>
    /// <param name="f"></param>
    private void DrawColumnLabel(ICanvas canvas, int x, Font f)
    {
        // "label" will be a character depending on the size of x and _firstColumn. If they are zero then "label" is "A".
        String label = ((char)('A' + x + _firstColumn)).ToString();
        SizeF size = canvas.GetStringSize(label, f, FONT_SIZE + FONT_SIZE * 1.75f);
        canvas.Font = f;
        canvas.FontSize = FONT_SIZE;
        canvas.DrawString(label,
              LABEL_COL_WIDTH + x * DATA_COL_WIDTH + (DATA_COL_WIDTH - size.Width) / 2,
              (LABEL_ROW_HEIGHT - size.Height) / 2, size.Width, size.Height,
              HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    /// <summary>
    /// Draws theAddress row label.  The rows are indexed beginning with zero.
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="y"></param>
    /// <param name="f"></param>
    private void DrawRowLabel(ICanvas canvas, int y, Font f)
    {
        // "label" will be a character depending on the size of y and _firstRow. If they are zero then "label" is 1.
        String label = (y + 1 + _firstRow).ToString();
        SizeF size = canvas.GetStringSize(label, f, FONT_SIZE + FONT_SIZE * 1.75f);
        canvas.Font = f;
        canvas.FontSize = FONT_SIZE;
        canvas.DrawString(label,
            LABEL_COL_WIDTH - size.Width - PADDING,
            LABEL_ROW_HEIGHT + y * DATA_ROW_HEIGHT + (DATA_ROW_HEIGHT - size.Height) / 2,
            size.Width, size.Height,
              HorizontalAlignment.Right, VerticalAlignment.Center);
    }
}
