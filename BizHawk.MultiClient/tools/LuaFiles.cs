﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BizHawk.MultiClient
{
	class LuaFile
	{
		public string Name;
		public string Path;
		public bool Enabled;
        public bool Paused;
		public bool IsSeparator;
		public LuaInterface.Lua Thread;
		public bool FrameWaiting;

		public LuaFile(string path)
		{
			Name = "";
			Path = path;
			Enabled = true;
            Paused = false;
			FrameWaiting = false;
		}

		public void Stop()
		{
			Enabled = false;
			Thread = null;
		}

		public LuaFile(string name, string path)
		{
			Name = name;
			Path = path;
			IsSeparator = false;
		}

		public LuaFile(bool isSeparator)
		{
			IsSeparator = isSeparator;
			Name = "";
			Path = "";
			Enabled = false;
		}

		public LuaFile(LuaFile l)
		{
			Name = l.Name;
			Path = l.Path;
			Enabled = l.Enabled;
            Paused = l.Paused;
			IsSeparator = l.IsSeparator;
		}

		public void Toggle()
		{
			Enabled ^= true;
            if (Enabled)
                Paused = false;
		}

        public void TogglePause()
        {
            Paused ^= true;
        }
	}
}
