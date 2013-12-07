﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using BizHawk.Client.Common;

namespace BizHawk.Client.EmuHawk
{
	public partial class TAStudio : Form, IToolForm
	{
		private const string MarkerColumnName = "MarkerColumn";
		private const string FrameColumnName = "FrameColumn";

		private int _defaultWidth;
		private int _defaultHeight;
		private TasMovie _tas;

		#region API

		public TAStudio()
		{
			InitializeComponent();
			TASView.QueryItemText += TASView_QueryItemText;
			TASView.QueryItemBkColor += TASView_QueryItemBkColor;
			TASView.VirtualMode = true;
			Closing += (o, e) =>
			{
				if (AskSave())
				{
					SaveConfigSettings();
					GlobalWin.OSD.AddMessage("TAStudio Disengaged");
					if (Global.MovieSession.Movie is TasMovie)
					{
						Global.MovieSession.Movie = new Movie();
					}
				}
				else
				{
					e.Cancel = true;
				}
			};

			TopMost = Global.Config.TAStudioTopMost;
		}

		public bool AskSave()
		{
			// TODO: eventually we want to do this
			return true;
		}

		public bool UpdateBefore
		{
			get { return false; }
		}

		public void UpdateValues()
		{
			if (!IsHandleCreated || IsDisposed)
			{
				return;
			}

			TASView.ItemCount = _tas.InputLogLength;
		}

		public void Restart()
		{
			if (!IsHandleCreated || IsDisposed)
			{
				return;
			}
		}

		#endregion

		private void TASView_QueryItemBkColor(int index, int column, ref Color color)
		{
			var record = _tas[index];
			if (!record.HasState)
			{
				color = BackColor;
			}
			else
			{
				color = record.Lagged ? Color.Pink : Color.LightGreen;
			}
		}

		private void TASView_QueryItemText(int index, int column, out string text)
		{
			try
			{
				var columnName = TASView.Columns[column].Name;
				var columnText = TASView.Columns[column].Text;

				if (columnName == MarkerColumnName)
				{
					text = String.Empty;
				}
				else if (columnName == FrameColumnName)
				{
					text = index.ToString().PadLeft(5, '0');
				}
				else
				{
					text = _tas[index].IsPressed(columnName) ? columnText : String.Empty;
				}
			}
			catch (Exception ex)
			{
				text = String.Empty;
				MessageBox.Show("oops\n" + ex);
			}
		}

		private void TAStudio_Load(object sender, EventArgs e)
		{
			if (Global.MovieSession.Movie.IsActive)
			{
				var result = MessageBox.Show("Warning, Tastudio doesn't support .bkm movie files at this time, opening this will cause you to lose your work, proceed? If you have unsaved changes you should cancel this, and savebefore opening TAStudio", "Unsupported movie", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
				if (result != DialogResult.Yes)
				{
					Close();
					return;
				}
			}

			GlobalWin.OSD.AddMessage("TAStudio engaged");
			Global.MovieSession.Movie = new TasMovie();
			_tas = Global.MovieSession.Movie as TasMovie;
			_tas.StartNewRecording();

			LoadConfigSettings();

			_tas.ActivePlayers = new List<string> { "Player 1", "Player 2" };
			SetUpColumns();
		}

		private void SetUpColumns()
		{
			TASView.Columns.Clear();
			AddColumn(MarkerColumnName, String.Empty, 18);
			AddColumn(FrameColumnName, "Frame#", 68);

			foreach (var kvp in _tas.AvailableMnemonics)
			{
				AddColumn(kvp.Key, kvp.Value.ToString(), 20);
			}
		}

		public void AddColumn(string columnName, string columnText, int columnWidth)
		{
			if (TASView.Columns[columnName] == null)
			{
				var column = new ColumnHeader
				{
					Name = columnName,
					Text = columnText,
					Width = columnWidth,
				};

				TASView.Columns.Add(column);
			}
		}

		private void LoadConfigSettings()
		{
			_defaultWidth = Size.Width;
			_defaultHeight = Size.Height;

			if (Global.Config.TAStudioSaveWindowPosition && Global.Config.TASWndx >= 0 && Global.Config.TASWndy >= 0)
			{
				Location = new Point(Global.Config.TASWndx, Global.Config.TASWndy);
			}

			if (Global.Config.TASWidth >= 0 && Global.Config.TASHeight >= 0)
			{
				Size = new Size(Global.Config.TASWidth, Global.Config.TASHeight);
			}
		}

		private void SaveConfigSettings()
		{
			Global.Config.TASWndx = Location.X;
			Global.Config.TASWndy = Location.Y;
			Global.Config.TASWidth = Right - Left;
			Global.Config.TASHeight = Bottom - Top;
		}

		#region Events

		#region File Menu

		private void ExitMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		#endregion

		#region Settings Menu
		
		private void SettingsSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			SaveWindowPositionMenuItem.Checked = Global.Config.TAStudioSaveWindowPosition;
			AutoloadMenuItem.Checked = Global.Config.AutoloadTAStudio;
			AlwaysOnTopMenuItem.Checked = Global.Config.TAStudioTopMost;
		}

		private void AutoloadMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.AutoloadTAStudio ^= true;
		}

		private void SaveWindowPositionMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.TAStudioSaveWindowPosition ^= true;
		}

		private void AlwaysOnTopMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.TAStudioTopMost ^= true;
		}

		#endregion

		#endregion
	}
}
