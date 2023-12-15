using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Security.AccessControl;
using System.Collections.Generic;
using static Common;
using System.Threading.Tasks.Dataflow;

public class Program {
    static void Main(string[] args) {
        new Common(args);
    }
}

public class Common {
    Tile[,] ProcessedCHR = new Tile[2, 256];

    Palette[,] AreaPalettes = new Palette[4, 4];
    Palette[,] EnemyPalettes = new Palette[4, 4];
    Palette[] AreaStylePalettes = new Palette[3];
    Palette BowserPalette;

    Dictionary<ushort, Bitmap> Metatiles = [];

    private BackgroundItem[,] BackSceneryData = new BackgroundItem[3, 48];
    private BackgroundElement[] BackSceneryMetatiles = new BackgroundElement[12];

    public Common(string[] args) {
        byte[] CHRRAW, PalettesRAW, MetatilesRAW, SceneryRAW;
        try {
            CHRRAW = File.ReadAllBytes(args.Length == 0 ? ".CHR" : args[0]);
            PalettesRAW = File.ReadAllBytes(args.Length == 0 ? "palettes.bin" : args[1]);
            MetatilesRAW = File.ReadAllBytes(args.Length == 0 ? "metatiles.bin" : args[2]);
            SceneryRAW = File.ReadAllBytes(args.Length == 0 ? "scenery.bin" : args[3]);
        } catch (Exception ex) {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return;
        }
        
        ProcessCHR(ref ProcessedCHR, ref CHRRAW);
        
        void NormalizePalettes() {
            ushort peek = 3;
            byte[] thisPalette = new byte[4];
            for (byte AreaType = 0; AreaType < 4; AreaType++) {
                for (byte AreaPalette = 0; AreaPalette < 4; AreaPalette++) {
                    for (byte PaletteColor = 0; PaletteColor < 4; PaletteColor++) {
                        thisPalette[PaletteColor] = PalettesRAW[peek++];
                    }
                    AreaPalettes[AreaType, AreaPalette] = new Palette(thisPalette, NESPALETTE);
                }

                for (byte SpritePalette = 0; SpritePalette < 4; SpritePalette++) {
                    for (byte PaletteColor = 0; PaletteColor < 4; PaletteColor++) {
                        thisPalette[PaletteColor] = PalettesRAW[peek++];
                    }
                    EnemyPalettes[AreaType, SpritePalette] = new Palette(thisPalette, NESPALETTE);
                }
                peek += 4;
            }
            for (byte AreaStyle = 0; AreaStyle < 3; AreaStyle++) {
                for (byte PaletteColor = 0; PaletteColor < 4; PaletteColor++) {
                    thisPalette[PaletteColor] = PalettesRAW[peek++];
                }
                AreaStylePalettes[AreaStyle] = new Palette(thisPalette, NESPALETTE);
                peek += 4;
            }
            for (byte BowserColor = 0; BowserColor < 4; BowserColor++) {
                thisPalette[BowserColor] = PalettesRAW[peek++];
            }
            BowserPalette = new Palette(thisPalette, NESPALETTE);
        };

        NormalizePalettes();

        void BuildMetatiles() {

            Bitmap BuildMetatile(Tile[] Tiles, Palette _Palette) {
                Bitmap Return = new Bitmap(16, 16);
                for (byte Width = 0, _Tile = 0; Width < 2; Width++) {
                    for (byte Height = 0; Height < 2; Height++, _Tile++) {
                        for (byte Y = 0; Y < 8; Y++) {
                            for (byte X = 0; X < 8; X++) {
                                Return.SetPixel((Width * 8) + X, (Height * 8) + Y, _Palette.Data[Tiles[_Tile].Data[Y, X]]);
                            }
                        }
                    }
                }
                return Return;
            }

            ushort peek = 0;
            Tile[] thisMetatile = new Tile[4];
            byte[] TileBeginnings = [0x27, 0x2E, 0x0A, 0x06];

            for (byte Metatile = 0; Metatile < TileBeginnings[0]; Metatile++) {
                for (byte _Tile = 0; _Tile < 4; _Tile++, peek++) {
                    thisMetatile[_Tile] = GetTile(false, MetatilesRAW[peek]);
                }
                for (byte AreaType = 0; AreaType < 4; AreaType++) {
                    Metatiles[(ushort)((AreaType << 8) |Metatile)] = BuildMetatile(thisMetatile, AreaPalettes[AreaType, 0]);
                }

                for (byte AreaStyle = 1; AreaStyle < 4; AreaStyle++) {
                    Metatiles[(ushort)((AreaStyle << 10) | Metatile)] = BuildMetatile(thisMetatile, AreaStylePalettes[AreaStyle-1]);
                }

                Metatiles[(ushort)((0x1000) | Metatile)] = BuildMetatile(thisMetatile, AreaPalettes[3, 0]);
            }

            for (byte _Palette = 1; _Palette < 4; _Palette++) {
                for (byte Metatile = 0; Metatile < TileBeginnings[_Palette]; Metatile++) {
                    for (byte _Tile = 0; _Tile < 4; _Tile++, peek++) {
                        thisMetatile[_Tile] = GetTile(false, MetatilesRAW[peek]);
                    }
                    for (byte AreaType = 0; AreaType < 4; AreaType++) {
                        Metatiles[(ushort)((AreaType << 8) | (_Palette << 6) | Metatile)] = BuildMetatile(thisMetatile, AreaPalettes[AreaType, _Palette]);
                    }

                    Metatiles[(ushort)((0x1000) | (_Palette << 6) | Metatile)] = BuildMetatile(thisMetatile, AreaPalettes[3, _Palette]);
                }
            }
        }

        void NormalizeScenery() {
            ushort peek = 3;
            for (byte BackgroundStyle = 0; BackgroundStyle < 3; BackgroundStyle ++) {
                for (byte _BackgroundItem = 0; _BackgroundItem < 48; _BackgroundItem++, peek++) {
                    BackSceneryData[BackgroundStyle, _BackgroundItem] = new BackgroundItem(SceneryRAW[peek]);
                }
            }

            for (byte Element = 0; Element < 12; Element++) {
                byte[] thisElement = new byte[3];
                for (byte _Metatile = 0; _Metatile < 3; _Metatile++, peek++) {
                    thisElement[_Metatile] = SceneryRAW[peek];
                }
                BackSceneryMetatiles[Element] = new BackgroundElement(thisElement);
            }
        }

        BuildMetatiles();

        foreach (KeyValuePair<ushort, Bitmap> kvp in Metatiles) { 

            kvp.Value.Save("./output/" + kvp.Key.ToString() + ".bmp");
        }
        
    }

    public class Tile {
        public byte[,] Data = new byte[8, 8];
        public Tile (ref byte[] CHRRAW, ushort peek) {
            for (byte row = 0; row < 8; row++, peek++) {
                for (byte col = 7; col != 0xff; col--) {    // underflow catching
                    Data[row, 7 - col] = (byte)((CHRRAW[peek] & (0b1 << col)) == 0 ? 0 : 1);
                    Data[row, 7 - col] |= (byte)((CHRRAW[peek+8] & (0b1 << col)) == 0 ? 0 : 2);
                }
            }
        }
    }

    public class Palette {
        public Color[] Data = new Color[4];
        public Palette(byte[] PaletteIDs, Color[] SYSPALETTE) {
            for (byte _Color = 0; _Color < 4; _Color++) {
                Data[_Color] = SYSPALETTE[PaletteIDs[_Color]];
            }
        }
    }

    void ProcessCHR(ref Tile[,] ProcessedCHR, ref byte[] CHRRAW){
        for (ushort _Tile = 0; _Tile < 512; _Tile++) {
            ProcessedCHR[_Tile >> 8, _Tile & 0xff] = new Tile(ref CHRRAW, (ushort)(_Tile << 4));
        }
    }

    public Tile GetTile(bool isOAM, byte ID){
        return ProcessedCHR[!isOAM ? 1 : 0, ID];
    }

    public Tile GetTile(bool isOAM, byte x, byte y){
        return ProcessedCHR[!isOAM ?  1 : 0, (y << 4) | x];
    }

    public Tile GetTile(ushort ID){
        return ProcessedCHR[ID >> 8, ID & 0xff];
    }

    public class BackgroundElement {
        public byte[] Data;
        public BackgroundElement(byte[] _Data) {
            Data = _Data;
        }
    }

    public class BackgroundItem {
        public byte y, Element;
        public BackgroundItem(byte Data) {
            y = (byte)(Data >> 4);
            Element = (byte)(Data & 0x0f);
        }
    }

    public BackgroundElement GetBackground(byte BackgroundStyle, byte BackgroundCollumn) {
        return BackSceneryMetatiles[BackSceneryData[BackgroundStyle, BackgroundCollumn].Element];
    }

    public Bitmap[] GetBackgroundMetatiles(byte BackgroundStyle, byte BackgroundCollumn, byte AreaType, byte AreaStyle, byte Foreground) {
        Bitmap[] Return = new Bitmap[3];
        if (Foreground == 0b111) {
            for (byte _Metatile = 0; _Metatile < 3; _Metatile++) {
                Return[_Metatile] = Metatiles[(ushort)((0x1000) | (BackSceneryMetatiles[BackSceneryData[BackgroundStyle, BackgroundCollumn].Element].Data[_Metatile]) & 0x3f)];
            }
        } else {
            for (byte _Metatile = 0; _Metatile < 3; _Metatile++) {
                byte thisMetatile = BackSceneryMetatiles[BackSceneryData[BackgroundStyle, BackgroundCollumn].Element].Data[_Metatile];
                Return[_Metatile] = Metatiles[(ushort)(((thisMetatile & 0xc0) != 0 ? AreaStyle << 10 : 0) | (AreaType << 8) | thisMetatile)];
            }
        }
        return Return;
    }

    public readonly static Color[] NESPALETTE = {
                        Color.FromArgb(0xff, 0x62, 0x62, 0x62), // grey         0x00
                        Color.FromArgb(0xff, 0x00, 0x1f, 0xb2), // dark blue    0x01
                        Color.FromArgb(0xff, 0x24, 0x04, 0xcb), // dark blue    0x02
                        Color.FromArgb(0xff, 0x52, 0x00, 0xb2), // purple       0x03
                        Color.FromArgb(0xff, 0x73, 0x00, 0x76), // purple       0x04
                        Color.FromArgb(0xff, 0x80, 0x00, 0x24), // magenta      0x05 
                        Color.FromArgb(0xff, 0x72, 0x0b, 0x00), // dark red     0x06
                        Color.FromArgb(0xff, 0x00, 0x52, 0x08), // brown        0x07
                        Color.FromArgb(0xff, 0x00, 0x24, 0x44), // gold         0x08
                        Color.FromArgb(0xff, 0x00, 0x57, 0x00), // dark green   0x09
                        Color.FromArgb(0xff, 0x00, 0x5c, 0x00), // dark green   0x0a
                        Color.FromArgb(0xff, 0x00, 0x53, 0x24), // turquoise    0x0b
                        Color.FromArgb(0xff, 0x00, 0x3c, 0x76), // navy blue    0x0c 
                        Color.FromArgb(0xff, 0x00, 0x00, 0x00), // black [X]    0x0d
                        Color.FromArgb(0xff, 0x00, 0x00, 0x00), // black        0x0e
                        Color.FromArgb(0xff, 0x00, 0x00, 0x00), // black        0x0f
                        Color.FromArgb(0xff, 0xab, 0xab, 0xab), // light grey   0x10
                        Color.FromArgb(0xff, 0x0d, 0x57, 0xff), // bold blue    0x11
                        Color.FromArgb(0xff, 0x4b, 0x30, 0xff), // bold blue    0x12
                        Color.FromArgb(0xff, 0x8a, 0x13, 0xff), // purple       0x13
                        Color.FromArgb(0xff, 0xbc, 0x08, 0xd6), // purple       0x14
                        Color.FromArgb(0xff, 0xd2, 0x12, 0x69), // bold pink    0x15 
                        Color.FromArgb(0xff, 0xc7, 0x2e, 0x00), // orange       0x16
                        Color.FromArgb(0xff, 0x9d, 0x54, 0x00), // brown        0x17 
                        Color.FromArgb(0xff, 0x60, 0x7b, 0x00), // gold         0x18
                        Color.FromArgb(0xff, 0x20, 0x98, 0x00), // green        0x19 
                        Color.FromArgb(0xff, 0x00, 0xa3, 0x00), // green        0x1a
                        Color.FromArgb(0xff, 0x00, 0x99, 0x42), // turquoise    0x1b
                        Color.FromArgb(0xff, 0x00, 0x7d, 0xb4), // blue         0x1c
                        Color.FromArgb(0xff, 0x00, 0x00, 0x00), // black        0x1d
                        Color.FromArgb(0xff, 0x00, 0x00, 0x00), // black        0x1e
                        Color.FromArgb(0xff, 0x00, 0x00, 0x00), // black        0x1f
                        Color.FromArgb(0xff, 0xff, 0xff, 0xff), // white        0x20
                        Color.FromArgb(0xff, 0x53, 0xae, 0xff), // light blue   0x21
                        Color.FromArgb(0xff, 0x90, 0x85, 0xff), // light blue   0x22
                        Color.FromArgb(0xff, 0xd3, 0x65, 0xff), // purple       0x23
                        Color.FromArgb(0xff, 0xff, 0x57, 0xff), // pink         0x24
                        Color.FromArgb(0xff, 0xff, 0x5d, 0xcf), // pink         0x25
                        Color.FromArgb(0xff, 0xff, 0x77, 0x57), // orange       0x26
                        Color.FromArgb(0xff, 0xfa, 0x9e, 0x00), // bold orange  0x27
                        Color.FromArgb(0xff, 0xbd, 0xc7, 0x00), // bold yellow  0x28
                        Color.FromArgb(0xff, 0x7a, 0xe7, 0x00), // bold green   0x29
                        Color.FromArgb(0xff, 0x43, 0xf6, 0x11), // bold green   0x2a
                        Color.FromArgb(0xff, 0x26, 0xef, 0x73), // turqiouse    0x2b
                        Color.FromArgb(0xff, 0x2c, 0xd5, 0xf5), // bold blue    0x2c
                        Color.FromArgb(0xff, 0x4e, 0x4e, 0x4e), // dark grey    0x2d
                        Color.FromArgb(0xff, 0x00, 0x00, 0x00), // black        0x2e
                        Color.FromArgb(0xff, 0x00, 0x00, 0x00), // black        0x2f
                        Color.FromArgb(0xff, 0xff, 0xff, 0xff), // white        0x30
                        Color.FromArgb(0xff, 0xb6, 0xe1, 0xff), // bright blue  0x31
                        Color.FromArgb(0xff, 0xce, 0xd1, 0xff), // bright blue  0x32
                        Color.FromArgb(0xff, 0xe9, 0xc3, 0xbf), // baby purple  0x33
                        Color.FromArgb(0xff, 0xff, 0xbc, 0xff), // baby pink    0x34
                        Color.FromArgb(0xff, 0xff, 0xbd, 0xf4), // baby pink    0x35
                        Color.FromArgb(0xff, 0xff, 0xc6, 0xc3), // baby pink    0x36 
                        Color.FromArgb(0xff, 0xff, 0x5d, 0x9a), // baby orange  0x37
                        Color.FromArgb(0xff, 0xe9, 0xe6, 0x81), // baby yellow  0x38
                        Color.FromArgb(0xff, 0xce, 0xf4, 0x81), // baby yellow  0x39
                        Color.FromArgb(0xff, 0xb6, 0xfb, 0x9a), // baby green   0x3a
                        Color.FromArgb(0xff, 0xa9, 0xfa, 0xc3), // baby blue    0x3b
                        Color.FromArgb(0xff, 0xa0, 0xf0, 0xf4), // baby blue    0x3c
                        Color.FromArgb(0xff, 0xb8, 0xb8, 0xb8), // light grey   0x3d
                        Color.FromArgb(0xff, 0x00, 0x00, 0x00), // black        0x3e
                        Color.FromArgb(0xff, 0x00, 0x00, 0x00), // black        0x3f
    };
}