﻿using System;
using System.IO;
using System.Diagnostics;

namespace BizHawk.Emulation.Consoles.Nintendo
{
	//(doesnt work in neshawk; works in fceux)

    /*
PCB Class: Unknown
iNES Mapper 242
PRG-ROM: 32KB
PRG-RAM: None
CHR-ROM: 16KB
CHR-RAM: None
Battery is not available
mirroring - both
     * 
     * Games:
     * Wai Xing Zhan Shi (Ch)
     */

    class Mapper242 : NES.NESBoardBase
    {
        int prg, mirror;
        
        public override bool Configure(NES.EDetectionOrigin origin)
        {
            //configure
			switch (Cart.board_type)
			{
				case "MAPPER242":
					break;
				default:
					return false;
			}
            return true;
        }

        public override byte ReadPRG(int addr)
        {
            return ROM[addr + (prg * 0x8000)];
        }

        public override void WritePRG(int addr, byte value)
        {
			mirror = (value & 0x03);
			prg = (addr >> 3) & 15;
			switch (mirror)
			{
				case 0: SetMirrorType(NES.NESBoardBase.EMirrorType.Vertical); break;
				case 1: SetMirrorType(NES.NESBoardBase.EMirrorType.Horizontal); break;
				case 2: SetMirrorType(NES.NESBoardBase.EMirrorType.OneScreenA); break;
				case 3: SetMirrorType(NES.NESBoardBase.EMirrorType.OneScreenB); break;
			}
        }

		public override void SyncState(Serializer ser)
        {
			base.SyncState(ser);
            ser.Sync("prg", ref prg);
            ser.Sync("mirror", ref mirror);
        }
    }
}
