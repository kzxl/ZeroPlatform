using System;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// ISO/IEC 16022 DataMatrix ECC200 Utah diagonal module placement and extraction engine.
    /// Maps between 2D boolean data module grids and 1D serialized byte codewords.
    /// </summary>
    public static class DataMatrixBitParser
    {
        /// <summary>
        /// Reads codeword bytes from a 2D data module grid using the ISO 16022 diagonal sweep traversal.
        /// </summary>
        public static byte[] ReadCodewords(bool[,] dataGrid, int expectedCodewords)
        {
            int numRows = dataGrid.GetLength(0);
            int numColumns = dataGrid.GetLength(1);
            byte[] result = new byte[expectedCodewords];
            int resultOffset = 0;

            bool[,] readMappingMatrix = new bool[numRows, numColumns];

            bool ReadModule(int row, int column)
            {
                if (row < 0)
                {
                    row += numRows;
                    column += 4 - ((numRows + 4) & 0x07);
                }
                if (column < 0)
                {
                    column += numColumns;
                    row += 4 - ((numColumns + 4) & 0x07);
                }
                if (row >= numRows)
                {
                    row -= numRows;
                }
                if (row >= 0 && row < numRows && column >= 0 && column < numColumns)
                {
                    readMappingMatrix[row, column] = true;
                    return dataGrid[row, column];
                }
                return false;
            }

            int ReadUtah(int row, int column)
            {
                int currentByte = 0;
                if (ReadModule(row - 2, column - 2)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(row - 2, column - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(row - 1, column - 2)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(row - 1, column - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(row - 1, column))     currentByte |= 1; currentByte <<= 1;
                if (ReadModule(row,     column - 2)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(row,     column - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(row,     column))     currentByte |= 1;
                return currentByte;
            }

            int ReadCorner1()
            {
                int currentByte = 0;
                if (ReadModule(numRows - 1, 0)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(numRows - 1, 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(numRows - 1, 2)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 2)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(1, numColumns - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(2, numColumns - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(3, numColumns - 1)) currentByte |= 1;
                return currentByte;
            }

            int ReadCorner2()
            {
                int currentByte = 0;
                if (ReadModule(numRows - 3, 0)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(numRows - 2, 0)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(numRows - 1, 0)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 4)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 3)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 2)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(1, numColumns - 1)) currentByte |= 1;
                return currentByte;
            }

            int ReadCorner3()
            {
                int currentByte = 0;
                if (ReadModule(numRows - 1, 0)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(numRows - 1, numColumns - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 3)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 2)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(1, numColumns - 3)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(1, numColumns - 2)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(1, numColumns - 1)) currentByte |= 1;
                return currentByte;
            }

            int ReadCorner4()
            {
                int currentByte = 0;
                if (ReadModule(numRows - 3, 0)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(numRows - 2, 0)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(numRows - 1, 0)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 2)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(0, numColumns - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(1, numColumns - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(2, numColumns - 1)) currentByte |= 1; currentByte <<= 1;
                if (ReadModule(3, numColumns - 1)) currentByte |= 1;
                return currentByte;
            }

            int row = 4;
            int column = 0;
            bool corner1Read = false, corner2Read = false, corner3Read = false, corner4Read = false;

            do
            {
                if ((row == numRows) && (column == 0) && !corner1Read)
                {
                    if (resultOffset < expectedCodewords) result[resultOffset++] = (byte)ReadCorner1();
                    row -= 2; column += 2; corner1Read = true;
                }
                else if ((row == numRows - 2) && (column == 0) && ((numColumns & 0x03) != 0) && !corner2Read)
                {
                    if (resultOffset < expectedCodewords) result[resultOffset++] = (byte)ReadCorner2();
                    row -= 2; column += 2; corner2Read = true;
                }
                else if ((row == numRows + 4) && (column == 2) && ((numColumns & 0x07) == 0) && !corner3Read)
                {
                    if (resultOffset < expectedCodewords) result[resultOffset++] = (byte)ReadCorner3();
                    row -= 2; column += 2; corner3Read = true;
                }
                else if ((row == numRows - 2) && (column == 0) && ((numColumns & 0x07) == 4) && !corner4Read)
                {
                    if (resultOffset < expectedCodewords) result[resultOffset++] = (byte)ReadCorner4();
                    row -= 2; column += 2; corner4Read = true;
                }
                else
                {
                    do
                    {
                        if ((row < numRows) && (column >= 0) && (row >= 0) && (column < numColumns) && !readMappingMatrix[row, column])
                        {
                            if (resultOffset < expectedCodewords) result[resultOffset++] = (byte)ReadUtah(row, column);
                        }
                        row -= 2; column += 2;
                    } while ((row >= 0) && (column < numColumns));
                    row += 1; column += 3;

                    do
                    {
                        if ((row >= 0) && (column < numColumns) && (row < numRows) && (column >= 0) && !readMappingMatrix[row, column])
                        {
                            if (resultOffset < expectedCodewords) result[resultOffset++] = (byte)ReadUtah(row, column);
                        }
                        row += 2; column -= 2;
                    } while ((row < numRows) && (column >= 0));
                    row += 3; column += 1;
                }
            } while ((row < numRows) || (column < numColumns));

            return result;
        }

        /// <summary>
        /// Places codeword bytes into a 2D data module grid using the ISO 16022 diagonal sweep traversal.
        /// </summary>
        public static void PlaceCodewords(byte[] codewords, bool[,] dataGrid)
        {
            int numRows = dataGrid.GetLength(0);
            int numColumns = dataGrid.GetLength(1);

            int[,] bits = new int[numRows, numColumns];
            for (int r = 0; r < numRows; r++)
                for (int c = 0; c < numColumns; c++)
                    bits[r, c] = -1;

            void SetBit(int r, int c, bool bit)
            {
                bits[r, c] = bit ? 1 : 0;
                dataGrid[r, c] = bit;
            }

            bool NoBit(int r, int c)
            {
                return bits[r, c] < 0;
            }

            void Module(int r, int c, int pos, int bitIndex)
            {
                if (r < 0)
                {
                    r += numRows;
                    c += 4 - ((numRows + 4) % 8);
                }
                if (c < 0)
                {
                    c += numColumns;
                    r += 4 - ((numColumns + 4) % 8);
                }
                if (r >= numRows)
                {
                    r -= numRows;
                }
                if (r >= 0 && r < numRows && c >= 0 && c < numColumns)
                {
                    int v = (pos < codewords.Length) ? codewords[pos] : 0;
                    bool b = ((v & (1 << (8 - bitIndex))) != 0);
                    SetBit(r, c, b);
                }
            }

            void Utah(int r, int c, int pos)
            {
                Module(r - 2, c - 2, pos, 1);
                Module(r - 2, c - 1, pos, 2);
                Module(r - 1, c - 2, pos, 3);
                Module(r - 1, c - 1, pos, 4);
                Module(r - 1, c,     pos, 5);
                Module(r,     c - 2, pos, 6);
                Module(r,     c - 1, pos, 7);
                Module(r,     c,     pos, 8);
            }

            void Corner1(int pos)
            {
                Module(numRows - 1, 0, pos, 1);
                Module(numRows - 1, 1, pos, 2);
                Module(numRows - 1, 2, pos, 3);
                Module(0, numColumns - 2, pos, 4);
                Module(0, numColumns - 1, pos, 5);
                Module(1, numColumns - 1, pos, 6);
                Module(2, numColumns - 1, pos, 7);
                Module(3, numColumns - 1, pos, 8);
            }

            void Corner2(int pos)
            {
                Module(numRows - 3, 0, pos, 1);
                Module(numRows - 2, 0, pos, 2);
                Module(numRows - 1, 0, pos, 3);
                Module(0, numColumns - 4, pos, 4);
                Module(0, numColumns - 3, pos, 5);
                Module(0, numColumns - 2, pos, 6);
                Module(0, numColumns - 1, pos, 7);
                Module(1, numColumns - 1, pos, 8);
            }

            void Corner3(int pos)
            {
                Module(numRows - 1, 0, pos, 1);
                Module(numRows - 1, numColumns - 1, pos, 2);
                Module(0, numColumns - 3, pos, 3);
                Module(0, numColumns - 2, pos, 4);
                Module(0, numColumns - 1, pos, 5);
                Module(1, numColumns - 3, pos, 6);
                Module(1, numColumns - 2, pos, 7);
                Module(1, numColumns - 1, pos, 8);
            }

            void Corner4(int pos)
            {
                Module(numRows - 3, 0, pos, 1);
                Module(numRows - 2, 0, pos, 2);
                Module(numRows - 1, 0, pos, 3);
                Module(0, numColumns - 2, pos, 4);
                Module(0, numColumns - 1, pos, 5);
                Module(1, numColumns - 1, pos, 6);
                Module(2, numColumns - 1, pos, 7);
                Module(3, numColumns - 1, pos, 8);
            }

            int posIdx = 0;
            int row = 4;
            int col = 0;

            do
            {
                if ((row == numRows) && (col == 0))
                {
                    Corner1(posIdx++);
                }
                if ((row == numRows - 2) && (col == 0) && ((numColumns % 4) != 0))
                {
                    Corner2(posIdx++);
                }
                if ((row == numRows + 4) && (col == 2) && ((numColumns % 8) == 0))
                {
                    Corner3(posIdx++);
                }
                if ((row == numRows - 2) && (col == 0) && ((numColumns % 8) == 4))
                {
                    Corner4(posIdx++);
                }

                do
                {
                    if ((row < numRows) && (col >= 0) && (row >= 0) && (col < numColumns) && NoBit(row, col))
                    {
                        Utah(row, col, posIdx++);
                    }
                    row -= 2;
                    col += 2;
                } while (row >= 0 && (col < numColumns));
                row += 1;
                col += 3;

                do
                {
                    if ((row >= 0) && (col < numColumns) && (row < numRows) && (col >= 0) && NoBit(row, col))
                    {
                        Utah(row, col, posIdx++);
                    }
                    row += 2;
                    col -= 2;
                } while ((row < numRows) && (col >= 0));
                row += 3;
                col += 1;

            } while ((row < numRows) || (col < numColumns));

            // Lastly, if the lower right-hand corner is untouched, fill in fixed pattern
            if (NoBit(numRows - 1, numColumns - 1))
            {
                SetBit(numRows - 1, numColumns - 1, true);
                SetBit(numRows - 2, numColumns - 2, true);
            }
        }
    }
}
