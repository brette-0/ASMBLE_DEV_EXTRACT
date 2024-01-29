using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Security.AccessControl;
using System.Collections.Generic;
using static Common;
using System.Threading.Tasks.Dataflow;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Linq.Expressions;




public class Program {
    static void Main(string[] args) {
        new Common(args);
    }
}

public class Common {
        const byte
            mushroomblock = 0,
            coinblock = 1,
            hiddencoinblock = 2,
            hiddenlifeblock = 3,
            mushroombrickblock = 4,
            vinebrickblock = 5,
            starbrickblock = 6,
            coinbrickblock = 7,
            lifebrickblock = 8,
            sidewaypipe = 9,
            usedblock = 10,
            springboard = 11,
            reversedpipe = 12,
            flagpole = 14,
            treething = 16, // expands to 16 + 0x0f
            horizontalbricks = 32,
            horizontalblock = 48,
            horizontalcoins = 60,
            verticalbricks = 72,
            verticalblocks = 84,
            pipenoenter = 96,
            pipeenter = 104,

            hole = 0,
            balancerope = 0x10,
            lowbridge = 0x20,
            middlebridge = 0x30,
            highbridge = 0x40,
            holewater = 0x50,
            lowqblockrow = 0x60,
            highqblockrow = 0x70,

            pageflag = 0x80,

            pageskip = 0,
            reversedpipe2 = 0x40,
            flagpole2 = 0x41,
            axe = 0x42,
            rope = 0x43,
            bowserbridge = 0x44,
            warpscrollstop = 0x45,
            scrollstop = 0x46,
            scrollstop2 = 0x47,
            flyingcheep = 0x48,
            cheepfrenzy = 0x49,
            frenzystop = 0x4a,
            loopcommand = 0x4b,
            backgroundchangenight = 0x4c,
            crashbypassable = 0x4d,
            crash = 0x4e, // expands to 50
            glitchobj1 = 0x51,
            crash2 = 0x52, // expands to 0x7f

            /* xe
                - : abcc dddd
                a = page flag
                b = hasalternate palette (mushroom)
                c = Background
                d = Terrain
            */

            // xf
            liftrope = 0, // expands to 0x0f
            liftropebalance = 0x10, // expands to 0x1f
            castle = 0x20, // expands to 2a
            castlecrash = 0x2b, // expands to 2f
            staircase = 0x30, // expands to 37
            fixedstaircase = 0x38,
            buggedstaircase = 0x39, // expands to 3f
            longreverselpipe = 0x40, // expands to 4f
            residualrope = 0x50; // expands to 5f



        const byte FGPositionSoftLimit = 0x0b;

        Tile[,] ProcessedCHR = new Tile[2, 256];
        Spawn[] PlayerSpawns = new Spawn[8];
        ushort[] Timers = new ushort[4];

        Palette[,] AreaPalettes = new Palette[4, 4];
        Palette[,] EnemyPalettes = new Palette[4, 4];
        Palette[] AreaStylePalettes = new Palette[3];
        Palette BowserPalette;

        Dictionary<ushort, Bitmap> Metatiles = [];
        Dictionary<ushort, Bitmap> EnemiesIMG = [];

        private BackgroundItem[,] BackSceneryData = new BackgroundItem[3, 48];
        private BackgroundElement[] BackSceneryMetatiles = new BackgroundElement[12];

        private byte[,] Foregrounds = new byte[3, 13];
        public ushort[] TerrainPatterns = new ushort[16];
        public byte[] TerrainMetatiles = new byte[4];

        public List<Area> Areas = [];
        public List<List<Enemy>> Enemies = [];

        List<byte[]> Worlds = [];
        byte[] EnemyAddrHOffsets = new byte[4];
        byte[] AreaDataHOffsets = new byte[4];

        public struct AreaObject {
            public byte x, y, id;  // page position and id
            public bool pageflag;

            public AreaObject(byte xy, byte id) : this() {
                this.x = (byte)(xy >> 4);
                this.y = (byte)(xy & 0x0f);
                this.id = (byte)(id & 0x7f);
                this.pageflag = (id & 0x80) == 0x80;
            }
        }

        public struct AreaPointer {
            byte world, page, area_id;

            public AreaPointer(byte pointer, ref byte area_id) : this() {
                this.world = (byte)(pointer >> 5);
                this.page = (byte)(pointer & 0x1f);
                this.page = area_id;
            }
        }

        public struct Enemy {
            byte x, y, id;  // enemy position
            AreaPointer? pointer;
            bool pageflag;
            bool hardmode;

            public Enemy(byte xy, byte id, byte? pointer) : this() {
                this.x = (byte)(xy >> 4);
                this.y = (byte)(xy & 0x0f);
                this.id = (byte)(xy & 0x3f);
                this.hardmode = (id & 0x40) == 0x40;
                this.pageflag = (id & 0x80) == 0x80;
                if (this.pointer != null) {
                    this.pointer = new AreaPointer(pointer ?? 0, ref id);
                }
            }
        }

        public class Area {
            public List<AreaObject> Objects;
            public byte Foreground, Background, Terrain, SpawnID;
            public bool introarea, spawndeath, playerwalk;
            public Spawn YPOS;
            public ushort Timer;

            public Area(Common parent, List<AreaObject> objects, byte yPosition, ushort timer, byte foreground, byte background, byte terrain) {
                YPOS = parent.PlayerSpawns[yPosition];
                Objects = objects;
                Timer = timer;
                Foreground = foreground;
                Background = background;
                Terrain = terrain;

                if (YPOS.isintro) {
                    introarea = true;
                    spawndeath = false;
                    playerwalk = !YPOS.hasBGdata;
                } else {
                    introarea = false;
                    spawndeath = timer == 0;
                    playerwalk = false;
                }
            }
        }


        struct LevelArea {
            Area AreaData;
            Enemy EnemyData;
        }

        public struct Spawn {
            byte y;
            public bool hasBGdata, isintro;
            public Spawn(byte y, bool hasBGdata, bool isintro) : this() {
                this.y = y; this.hasBGdata = hasBGdata; this.isintro = isintro;
            }
        }

    public Common(string[] args) {
        byte[] CHRRAW, PalettesRAW, MetatilesRAW, SceneryRAW, EnemyRAW, spawningRAW;
        string[] LevelASM = File.ReadAllLines(args.Length == 0 ? "leveldata.asm" : args[6]);
        try {
            CHRRAW = File.ReadAllBytes(args.Length == 0 ? ".CHR" : args[0]);
            PalettesRAW = File.ReadAllBytes(args.Length == 0 ? "palettes.bin" : args[1]);
            MetatilesRAW = File.ReadAllBytes(args.Length == 0 ? "metatiles.bin" : args[2]);
            SceneryRAW = File.ReadAllBytes(args.Length == 0 ? "scenery.bin" : args[3]);
            EnemyRAW = File.ReadAllBytes(args.Length == 0 ? "enemydata.bin" : args[4]);
            spawningRAW = File.ReadAllBytes(args.Length == 0 ? "spawning.bin" : args[5]);
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
                    AreaPalettes[AreaType, AreaPalette] = new Palette(thisPalette, Common.NESPALETTE);
                }

                for (byte SpritePalette = 0; SpritePalette < 4; SpritePalette++) {
                    for (byte PaletteColor = 0; PaletteColor < 4; PaletteColor++) {
                        thisPalette[PaletteColor] = PalettesRAW[peek++];
                    }
                    EnemyPalettes[AreaType, SpritePalette] = new Palette(thisPalette, Common.NESPALETTE);
                }
                peek += 4;
            }
            for (byte AreaStyle = 0; AreaStyle < 3; AreaStyle++) {
                for (byte PaletteColor = 0; PaletteColor < 4; PaletteColor++) {
                    thisPalette[PaletteColor] = PalettesRAW[peek++];
                }
                AreaStylePalettes[AreaStyle] = new Palette(thisPalette, Common.NESPALETTE);
                peek += 4;
            }
            for (byte BowserColor = 0; BowserColor < 4; BowserColor++) {
                thisPalette[BowserColor] = PalettesRAW[peek++];
            }
            BowserPalette = new Palette(thisPalette, Common.NESPALETTE);
        };
        void BuildMetatiles() {

            Bitmap BuildMetatile(Tile[] Tiles, Palette _Palette) {
                Bitmap Return = new(16, 16);
                Return.MakeTransparent();
                for (byte Width = 0, _Tile = 0; Width < 2; Width++) {
                    for (byte Height = 0; Height < 2; Height++, _Tile++) {
                        for (byte Y = 0; Y < 8; Y++) {
                            for (byte X = 0; X < 8; X++) {
                                byte thisBit = Tiles[_Tile].Data[Y, X];
                                if (thisBit == 0) continue;
                                Return.SetPixel((Width * 8) + X, (Height * 8) + Y, _Palette.Data[thisBit]);
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
                    Metatiles[(ushort)((AreaType << 8) | Metatile)] = BuildMetatile(thisMetatile, AreaPalettes[AreaType, 0]);
                }

                for (byte AreaStyle = 1; AreaStyle < 4; AreaStyle++) {
                    Metatiles[(ushort)((AreaStyle << 10) | Metatile)] = BuildMetatile(thisMetatile, AreaStylePalettes[AreaStyle - 1]);
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
            for (byte BackgroundStyle = 0; BackgroundStyle < 3; BackgroundStyle++) {
                for (byte _BackgroundItem = 0; _BackgroundItem < 48; _BackgroundItem++, peek++) {
                    if (SceneryRAW[peek] != 0) BackSceneryData[BackgroundStyle, _BackgroundItem] = new BackgroundItem(SceneryRAW[peek]);
                }
            }

            for (byte Element = 0; Element < 12; Element++) {
                byte[] thisElement = new byte[3];
                for (byte _Metatile = 0; _Metatile < 3; _Metatile++, peek++) {
                    thisElement[_Metatile] = SceneryRAW[peek];
                }
                BackSceneryMetatiles[Element] = new BackgroundElement(thisElement);
            }
            peek += 3;
            for (byte Foreground = 0; Foreground < 3; Foreground++) {
                for (byte _Metatile = 0; _Metatile < 13; _Metatile++, peek++) {
                    Foregrounds[Foreground, _Metatile] = SceneryRAW[peek];
                }
            }

            for (byte Metatile = 0; Metatile < 4; Metatile++, peek++) {
                TerrainMetatiles[Metatile] = SceneryRAW[peek];
            }

            for (byte TerrainByte = 0; TerrainByte < 32; TerrainByte++, peek++) {
                TerrainPatterns[TerrainByte >> 1] |= SceneryRAW[peek];
                if (TerrainByte % 2 == 0) TerrainPatterns[TerrainByte >> 1] <<= 8;
            }
        }
        void NormalizeEnemies() {

            Bitmap BuildSpriteTile(Tile[] Tiles, Palette _Palette) {
                Bitmap Return = new(16, 24);
                Return.MakeTransparent();
                for (byte Height = 0, _Tile = 0; Height < 3; Height++) {
                    for (byte Width = 0; Width < 2; Width++, _Tile++) {
                        for (byte Y = 0; Y < 8; Y++) {
                            for (byte X = 0; X < 8; X++) {
                                byte thisBit = Tiles[_Tile].Data[Y, X];
                                if (thisBit == 0) continue;
                                Return.SetPixel((Width * 8) + X, (Height * 8) + Y, _Palette.Data[thisBit]);
                            }
                        }
                    }
                }
                return Return;
            }
            for (byte EnemyID = 0; EnemyID < 27; EnemyID++) {
                Tile[] thisEnemy = new Tile[6];
                for (byte _Tile = 0; _Tile < 6; _Tile++) {
                    thisEnemy[_Tile] = ProcessedCHR[0, EnemyRAW[EnemyRAW[258 + EnemyID] + _Tile]];
                }
                for (byte AreaType = 0; AreaType < 3; AreaType++) {
                    Bitmap Result = BuildSpriteTile(thisEnemy, EnemyPalettes[AreaType, EnemyRAW[285 + EnemyID] & 0b11]);
                    Result.Save(AreaType.ToString() + EnemyID.ToString() + ".bmp");
                    EnemiesIMG[(ushort)((AreaType << 8) | EnemyID)] = BuildSpriteTile(thisEnemy, EnemyPalettes[AreaType, EnemyRAW[285 + EnemyID] & 0b11]);
                }
            }
        }

        Area NormalizeAreaData(byte[] LevelData) {
            /*
                Normalize Level Information (and header)
            */

            ushort thisTimer = Timers[(byte)(LevelData[0] >> 6)];
            byte thisYpos = (byte)((LevelData[0] >> 3) & 0b11);
            byte thisForeground = (byte)(LevelData[0] & 0b111);

            bool cloud = (LevelData[1] & 0x80) == 0x80;
            bool altpal = (LevelData[1] & 0x40) == 0x40;
            byte thisBG = (byte)((LevelData[1] >> 4) & 0b11);
            byte thisTerrain = (byte)(LevelData[1] & 0x0f);

            List<AreaObject> thisObjects = [];

            for (ushort offset = 2; offset < LevelData.Length; offset += 2) {
                thisObjects.Add(new AreaObject(LevelData[offset], LevelData[offset + 1]));
            }
            return new Area(this, thisObjects, thisYpos, thisTimer, thisForeground, thisBG, thisTerrain);

        }

        List<Enemy> NormalizeEnemyData(byte[] EnemyData) {
            List<Enemy> thisEnemies = [];

            for (ushort offset = 0; offset < EnemyData.Length;) {
                thisEnemies.Add(new Enemy(EnemyData[offset], EnemyData[offset + 1], (EnemyData[offset] & 0x0f) == 0x0e ? EnemyData[offset + 2] : null));
                offset += (ushort)((EnemyData[offset] & 0x0f) == 0x0e ? 3 : 2);
            }
            return thisEnemies;
        }

        void NormalizeLevels() {
            int offset = 0, temp = 0;
            byte worlds = 0;

            for (; LevelASM[offset] != "AreaAddrOffsets:"; offset++) { }
            temp = ++offset;    // points to first world
            for (; LevelASM[offset] != "; END OF AreaAddrOffsets"; offset++) { }
            
            worlds = (byte)(offset - temp);   // points to after last world

            for (byte world = 0; world < worlds; world++) {
                Worlds.Add(Common.PsuedoAssemble(LevelASM[temp + world]));
            }

            for (; LevelASM[offset] != "EnemyAddrHOffsets:"; offset++) { }
            EnemyAddrHOffsets = Common.PsuedoAssemble(LevelASM[++offset]);
            for (; LevelASM[offset] != "AreaDataHOffsets:"; offset++) { }
            AreaDataHOffsets = Common.PsuedoAssemble(LevelASM[++offset]);
            for (; LevelASM[offset] != "; ENEMY DATA BEGIN"; offset++) { }

            byte progress = 0;

            while (LevelASM[offset] != "; OBJECT DATA BEGIN") {
                while (!LevelASM[offset].Contains(".db")) { offset++; }
                
                List<byte> thisbinary = [];
                while (!LevelASM[offset].Contains(".db $ff")) {
                    thisbinary.AddRange(Common.PsuedoAssemble(LevelASM[offset++]));
                }
                offset++;
                while (!(LevelASM[offset] == "; OBJECT DATA BEGIN" || LevelASM[offset].Contains(".db"))) ++offset;
                Enemies.Add(NormalizeEnemyData([.. thisbinary]));
                progress++;
            }
            progress = 0;
            while (LevelASM[offset] != "; END") {
                while (!LevelASM[offset].Contains(".db")) { offset++; }

                List<byte> thisbinary = [];
                while (!LevelASM[offset].Contains(".db $fd")) {
                    thisbinary.AddRange(Common.PsuedoAssemble(LevelASM[offset++]));
                }
                offset++;
                while (!(LevelASM[offset] == "; END" || LevelASM[offset].Contains(".db"))) ++offset;
                Areas.Add(NormalizeAreaData([.. thisbinary]));
                progress++;
            }
        }

        void NormalizeSpawns() {
            for (byte spawnid = 0; spawnid < 8; spawnid++) {
                PlayerSpawns[spawnid] = new Spawn(spawningRAW[7 + spawnid], spawningRAW[16 + spawnid] != 0x00, spawnid == 6);
            }
        }

        void NormalizeTimers() {
            for (byte timerid = 0; timerid < 4; timerid++) {
                Timers[timerid] = (ushort)(100 * spawningRAW[23 + timerid]);
            }
        }

        NormalizePalettes();
        BuildMetatiles();
        NormalizeScenery();
        NormalizeEnemies();

        NormalizeSpawns();
        NormalizeTimers();
        NormalizeLevels();
    }

    public Bitmap GetMetatile(byte AreaType, byte AreaStyle, byte MetatileID, bool CastleOverride) {
        if (CastleOverride) return Metatiles[(ushort)(0x1000 | MetatileID)];
        if ((MetatileID & 0xc0) != 0) AreaStyle = 0;
        else if (AreaStyle != 0) AreaType = 0;
        return Metatiles[(ushort)((AreaStyle << 10) | (AreaType << 8) | MetatileID)];
    }
    public Bitmap[]? GetBackgroundMetatiles(byte Background, byte Collumn, byte AreaType, byte AreaStyle, bool CastleOverride) {
        Bitmap[] thisCollumn = new Bitmap[3];
        if (BackSceneryData[Background, Collumn] == null) return null;
        for (byte Metatile = 0; Metatile < 3; Metatile++) {
            thisCollumn[Metatile] = GetMetatile(AreaType, AreaStyle, BackSceneryMetatiles[BackSceneryData[Background, Collumn].Element].Data[Metatile], CastleOverride);
        }
        return thisCollumn;
    }
    public Bitmap[] GetForegroundMetatiles(byte Foreground, byte AreaType, byte AreaStyle, bool CastleOverride) {
        Bitmap[] Return = new Bitmap[13];
        for (byte Metatile = 0; Metatile < 13; Metatile++) {
            Return[Metatile] = GetMetatile(AreaType, AreaStyle, Foregrounds[Foreground, Metatile], CastleOverride);
        }
        return Return;
    }
    public Bitmap[] GetTerrain(byte Terrain, byte AreaType, byte AreaStyle, bool CastleOverride) {
        Bitmap[] Return = new Bitmap[13];
        for (byte Metatile = 0; Metatile < 13; Metatile++) {
            if ((Metatile & 8) == 0) {
                if (((TerrainPatterns[Terrain] >> 8) & (0b1 << Metatile)) == 0) continue;
            } else {
                if (((TerrainPatterns[Terrain] & 0xff) & (0b1 << (Metatile - 8))) == 0) continue;
            }
            Return[Metatile] = GetMetatile(AreaType, AreaStyle, TerrainMetatiles[AreaType], CastleOverride);
        }
        return Return;
    }

    public List<Enemy> getEnemyData(byte Palette, byte Lower) {
        return Enemies[EnemyAddrHOffsets[Palette] + Lower];
    }

    public Area GetAreaData(byte Palette, byte Lower) {
        return Areas[AreaDataHOffsets[Palette] + Lower];
    }

    public class Tile {
        public byte[,] Data = new byte[8, 8];
        public Tile(ref byte[] CHRRAW, ushort peek) {
            for (byte row = 0; row < 8; row++, peek++) {
                for (byte col = 7; col != 0xff; col--) {    // underflow catching
                    Data[row, 7 - col] = (byte)((CHRRAW[peek] & (0b1 << col)) == 0 ? 0 : 1);
                    Data[row, 7 - col] |= (byte)((CHRRAW[peek + 8] & (0b1 << col)) == 0 ? 0 : 2);
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

    void ProcessCHR(ref Tile[,] ProcessedCHR, ref byte[] CHRRAW) {
        for (ushort _Tile = 0; _Tile < 512; _Tile++) {
            ProcessedCHR[_Tile >> 8, _Tile & 0xff] = new Tile(ref CHRRAW, (ushort)(_Tile << 4));
        }
    }

    public Tile GetTile(bool isOAM, byte ID) {
        return ProcessedCHR[!isOAM ? 1 : 0, ID];
    }
    public Tile GetTile(bool isOAM, byte x, byte y) {
        return ProcessedCHR[!isOAM ? 1 : 0, (y << 4) | x];
    }
    public Tile GetTile(ushort ID) {
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
            Element = (byte)(--Data & 0x0f);
        }
    }

    public readonly static Color[] NESPALETTE = [
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
    ];

    public static byte[] PsuedoAssemble(string asm) {
        List<byte> byteList = new List<byte>();
        Func<char, bool> IsHexDigit = (c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        for (int i = 0; i < asm.Length; i++) {
            if (asm[i] == '$') {
                // Check if there are at least two more characters after '$'
                if (i + 2 < asm.Length) {
                    // Check if the next two characters form a valid hex pair
                    if (IsHexDigit(asm[i + 1]) && IsHexDigit(asm[i + 2])) {
                        string hexValue = asm.Substring(i + 1, 2);
                        byteList.Add(Convert.ToByte(hexValue, 16));
                        i += 2; // Skip the processed hex characters
                    } else {
                        // Handle case where '$' is not followed by two valid hex digits
                        // You might want to log an error or take other appropriate actions here
                    }
                } else {
                    // Handle case where there are not enough characters after '$'
                    // You might want to log an error or take other appropriate actions here
                }
            }
        }

        return byteList.ToArray();
    }
}