using System.Drawing;

internal static class PlotLayout
{
    private const int Columns = 3, RowHeight = 216;

    internal static Rectangle Cell(float scale, int position)
    {
        int row = position / Columns, column = position % Columns;
        // Neighboring plots meet at the end of a row as the grid wraps.
        if (row % 2 != 0) column = Columns - 1 - column;
        return Bounds(scale, 236 * column, RowHeight * row, 226, 204);
    }

    internal static int ScrollHeight(float scale, int count)
    {
        return (int)(((count + Columns - 1) / Columns) * RowHeight * scale);
    }

    internal static Rectangle Bounds(float scale, int x, int y, int width, int height)
    {
        return new Rectangle((int)(x * scale), (int)(y * scale), (int)(width * scale), (int)(height * scale));
    }
}
