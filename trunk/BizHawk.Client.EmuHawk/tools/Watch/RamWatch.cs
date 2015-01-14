﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;

using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk.WinFormExtensions;
using BizHawk.Client.EmuHawk.ToolExtensions;

namespace BizHawk.Client.EmuHawk
{
	public partial class RamWatch : Form, IToolForm
	{
		private WatchList _watches;

		private int _defaultWidth;
		private int _defaultHeight;
		private string _sortedColumn = string.Empty;
		private bool _sortReverse;
		private bool _paused = false;

		[RequiredService]
		private IMemoryDomains _memoryDomains { get; set; }

		[RequiredService]
		private IEmulator _emu { get; set; }

		[ConfigPersist]
		public RamWatchSettings Settings { get; set; }

		public class RamWatchSettings : ToolDialogSettings
		{
			public RamWatchSettings()
			{
				Columns = new ColumnList
				{
					new Column { Name = WatchList.ADDRESS, Visible = true, Index = 0, Width = 60 },
					new Column { Name = WatchList.VALUE, Visible = true, Index = 1, Width = 59 },
					new Column { Name = WatchList.PREV, Visible = false, Index = 2, Width = 59 },
					new Column { Name = WatchList.CHANGES, Visible = true, Index = 3, Width = 55 },
					new Column { Name = WatchList.DIFF, Visible = false, Index = 4, Width = 59 },
					new Column { Name = WatchList.DOMAIN, Visible = true, Index = 5, Width = 55 },
					new Column { Name = WatchList.NOTES, Visible = true, Index = 6, Width = 128 },
				};
			}

			public ColumnList Columns { get; set; }
		}

		public RamWatch()
		{
			InitializeComponent();
			Settings = new RamWatchSettings();

			WatchListView.QueryItemText += WatchListView_QueryItemText;
			WatchListView.QueryItemBkColor += WatchListView_QueryItemBkColor;
			WatchListView.VirtualMode = true;
			Closing += (o, e) =>
			{
				if (AskSaveChanges())
				{
					SaveConfigSettings();
				}
				else
				{
					e.Cancel = true;
				}
			};

			_sortedColumn = string.Empty;
			_sortReverse = false;
		}

		private IEnumerable<int> SelectedIndices
		{
			get { return WatchListView.SelectedIndices.Cast<int>(); }
		}

		private IEnumerable<Watch> SelectedItems
		{
			get { return SelectedIndices.Select(index => _watches[index]); }
		}

		private IEnumerable<Watch> SelectedWatches
		{
			get { return SelectedItems.Where(x => !x.IsSeparator); }
		}

		#region Properties

		public IEnumerable<Watch> Watches
		{
			get
			{
				return _watches.Where(x => !x.IsSeparator);
			}
		}

		public bool UpdateBefore
		{
			get { return false; }
		}

		#endregion

		#region API

		public void AddWatch(Watch watch)
		{
			_watches.Add(watch);
			WatchListView.ItemCount = _watches.ItemCount;
			UpdateValues();
			UpdateWatchCount();
			Changes();
		}

		public bool AskSaveChanges()
		{
			if (_watches.Changes)
			{
				GlobalWin.Sound.StopSound();
				var result = MessageBox.Show("Save Changes?", "Ram Watch", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button3);
				GlobalWin.Sound.StartSound();
				if (result == DialogResult.Yes)
				{
					if (string.IsNullOrWhiteSpace(_watches.CurrentFileName))
					{
						SaveAs();
					}
					else
					{
						_watches.Save();
					}
				}
				else if (result == DialogResult.No)
				{
					_watches.Changes = false;
					return true;
				}
				else if (result == DialogResult.Cancel)
				{
					return false;
				}
			}

			return true;
		}

		public void LoadFileFromRecent(string path)
		{
			var ask_result = true;
			if (_watches.Changes)
			{
				ask_result = AskSaveChanges();
			}

			if (ask_result)
			{
				var load_result = _watches.Load(path, append: false);
				if (!load_result)
				{
					Global.Config.RecentWatches.HandleLoadError(path);
				}
				else
				{
					Global.Config.RecentWatches.Add(path);
					WatchListView.ItemCount = _watches.ItemCount;
					UpdateWatchCount();
					UpdateStatusBar();
					_watches.Changes = false;
				}
			}
		}

		public void LoadWatchFile(FileInfo file, bool append)
		{
			if (file != null)
			{
				var result = true;
				if (_watches.Changes)
				{
					result = AskSaveChanges();
				}

				if (result)
				{
					_watches.Load(file.FullName, append);
					WatchListView.ItemCount = _watches.ItemCount;
					UpdateWatchCount();
					Global.Config.RecentWatches.Add(_watches.CurrentFileName);
					SetMemoryDomain(_watches.Domain.ToString());
					UpdateStatusBar();
				}
			}
		}

		public void Restart()
		{
			if ((!IsHandleCreated || IsDisposed) && !Global.Config.DisplayRamWatch)
			{
				return;
			}

			if (_watches != null && !string.IsNullOrWhiteSpace(_watches.CurrentFileName))
			{
				_watches.RefreshDomans(_memoryDomains, _memoryDomains.MainMemory);
				_watches.Reload();
				SetPlatformAndMemoryDomainLabel();
				UpdateStatusBar();
			}
			else
			{
				_watches = new WatchList(_memoryDomains, _memoryDomains.MainMemory, _emu.SystemId);
				NewWatchList(true);
			}
		}

		public void UpdateValues()
		{
			if (_paused)
			{
				return;
			}

			if ((!IsHandleCreated || IsDisposed) && !Global.Config.DisplayRamWatch)
			{
				return;
			}

			if (_watches.Any())
			{
				_watches.UpdateValues();

				if (Global.Config.DisplayRamWatch)
				{
					for (var i = 0; i < _watches.Count; i++)
					{
						var frozen = !_watches[i].IsSeparator && Global.CheatList.IsActive(_watches[i].Domain, _watches[i].Address ?? 0);
						GlobalWin.OSD.AddGUIText(
							_watches[i].ToString(),
							Global.Config.DispRamWatchx,
							Global.Config.DispRamWatchy + (i * 14),
							Color.Black,
							frozen ? Color.Cyan : Color.White,
							0
						);
					}
				}

				if (!IsHandleCreated || IsDisposed)
				{
					return;
				}

				WatchListView.BlazingFast = true;
				WatchListView.UseCustomBackground = NeedsBackground;
				WatchListView.Invalidate();
				WatchListView.BlazingFast = false;
			}
		}

		private bool NeedsBackground
		{
			get
			{
				foreach(var watch in _watches)
				{
					if (watch.IsSeparator)
					{
						return true;
					}

					if (Global.CheatList.IsActive(_watches.Domain, watch.Address ?? 0))
					{
						return true;
					}

					if (watch.IsOutOfRange)
					{
						return true;
					}
				}

				return false;
			}
		}

		public void FastUpdate()
		{
			if (_paused)
			{
				return;
			}

			if ((!IsHandleCreated || IsDisposed) && !Global.Config.DisplayRamWatch)
			{
				return;
			}

			if (_watches.Any())
			{
				_watches.UpdateValues();

				if (Global.Config.DisplayRamWatch)
				{
					for (var i = 0; i < _watches.Count; i++)
					{
						var frozen = !_watches[i].IsSeparator && Global.CheatList.IsActive(_watches[i].Domain, _watches[i].Address ?? 0);
						GlobalWin.OSD.AddGUIText(
							_watches[i].ToString(),
							Global.Config.DispRamWatchx,
							Global.Config.DispRamWatchy + (i * 14),
							Color.Black,
							frozen ? Color.Cyan : Color.White,
							0
						);
					}
				}
			}
		}

		#endregion

		#region Private Methods

		private void Changes()
		{
			_watches.Changes = true;
			UpdateStatusBar();
		}

		private void CopyWatchesToClipBoard()
		{
			var indexes = SelectedIndices.ToList();

			if (indexes.Any())
			{
				var sb = new StringBuilder();
				foreach (var index in indexes)
				{
					foreach (ColumnHeader column in WatchListView.Columns)
					{
						sb.Append(GetColumnValue(column.Name, index)).Append('\t');
					}

					sb.Remove(sb.Length - 1, 1);
					sb.AppendLine();
				}

				if (sb.Length > 0)
				{
					Clipboard.SetDataObject(sb.ToString());
				}
			}
		}

		private void EditWatch(bool duplicate = false)
		{
			var indexes = SelectedIndices.ToList();

			if (SelectedWatches.Any())
			{
				var we = new WatchEditor
				{
					InitialLocation = this.ChildPointToScreen(WatchListView),
					MemoryDomains = _memoryDomains
				};

				we.SetWatch(_watches.Domain, SelectedWatches, duplicate ? WatchEditor.Mode.Duplicate : WatchEditor.Mode.Edit);
				
				var result = we.ShowHawkDialog();
				if (result == DialogResult.OK)
				{
					Changes();
					if (duplicate)
					{
						_watches.AddRange(we.Watches);
						WatchListView.ItemCount = _watches.ItemCount;
					}
					else
					{
						for (var i = 0; i < we.Watches.Count; i++)
						{
							_watches[indexes[i]] = we.Watches[i];
						}
					}
				}

				UpdateValues();
			}
		}

		private string GetColumnValue(string name, int index)
		{
			switch (name)
			{
				default:
					return string.Empty;
				case WatchList.ADDRESS:
					return _watches[index].AddressString;
				case WatchList.VALUE:
					return _watches[index].ValueString;
				case WatchList.PREV:
					return _watches[index].PreviousStr;
				case WatchList.CHANGES:
					return _watches[index].ChangeCount.ToString();
				case WatchList.DIFF:
					return _watches[index].Diff;
				case WatchList.DOMAIN:
					return _watches[index].Domain.Name;
				case WatchList.NOTES:
					return _watches[index].Notes;
			}
		}

		private void LoadColumnInfo()
		{
			WatchListView.Columns.Clear();

			var columns = Settings.Columns
				.Where(c => c.Visible)
				.OrderBy(c => c.Index);

			foreach (var column in columns)
			{
				WatchListView.AddColumn(column);
			}
		}

		private void LoadConfigSettings()
		{
			// Size and Positioning
			_defaultWidth = Size.Width;
			_defaultHeight = Size.Height;

			if (Settings.UseWindowPosition)
			{
				Location = Settings.WindowPosition;
			}

			if (Settings.UseWindowSize)
			{
				Size = Settings.WindowSize;
			}

			LoadColumnInfo();
		}

		private void NewWatchList(bool suppressAsk)
		{
			var result = true;
			if (_watches.Changes)
			{
				result = AskSaveChanges();
			}

			if (result || suppressAsk)
			{
				_watches.Clear();
				WatchListView.ItemCount = _watches.ItemCount;
				UpdateWatchCount();
				UpdateStatusBar();
				_sortReverse = false;
				_sortedColumn = string.Empty;
			}
		}

		private void OrderColumn(int index)
		{
			var column = WatchListView.Columns[index];
			if (column.Name != _sortedColumn)
			{
				_sortReverse = false;
			}

			_watches.OrderWatches(column.Name, _sortReverse);

			_sortedColumn = column.Name;
			_sortReverse ^= true;
			WatchListView.Refresh();
		}

		private void SaveAs()
		{
			var result = _watches.SaveAs(ToolHelpers.GetWatchSaveFileFromUser(_watches.CurrentFileName));
			if (result)
			{
				UpdateStatusBar(saved: true);
				Global.Config.RecentWatches.Add(_watches.CurrentFileName);
			}
		}

		private void SaveColumnInfo()
		{
			foreach (ColumnHeader column in WatchListView.Columns)
			{
				Settings.Columns[column.Name].Index = column.DisplayIndex;
				Settings.Columns[column.Name].Width = column.Width;
			}
		}

		private void SaveConfigSettings()
		{
			SaveColumnInfo();
			Settings.Wndx = Location.X;
			Settings.Wndy = Location.Y;
			Settings.Width = Right - Left;
			Settings.Height = Bottom - Top;
		}

		private void SetMemoryDomain(string name)
		{
			_watches.Domain = _memoryDomains[name];
			SetPlatformAndMemoryDomainLabel();
			Update();
		}

		private void SetPlatformAndMemoryDomainLabel()
		{
			MemDomainLabel.Text = _emu.SystemId + " " + _watches.Domain.Name;
		}

		private void UpdateStatusBar(bool saved = false)
		{
			var message = string.Empty;
			if (!string.IsNullOrWhiteSpace(_watches.CurrentFileName))
			{
				if (saved)
				{
					message = Path.GetFileName(_watches.CurrentFileName) + " saved.";
				}
				else
				{
					message = Path.GetFileName(_watches.CurrentFileName) + (_watches.Changes ? " *" : string.Empty);
				}
			}

			ErrorIconButton.Visible = _watches.Where(watch => !watch.IsSeparator).Any(watch => (watch.Address ?? 0) >= watch.Domain.Size);

			MessageLabel.Text = message;
		}

		private void UpdateWatchCount()
		{
			WatchCountLabel.Text = _watches.WatchCount + (_watches.WatchCount == 1 ? " watch" : " watches");
		}

		private void WatchListView_QueryItemBkColor(int index, int column, ref Color color)
		{
			if (index >= _watches.ItemCount)
			{
				return;
			}

			if (column == 0)
			{
				if (_watches[index].IsSeparator)
				{
					color = BackColor;
				}
				else if (_watches[index].Address.Value >= _watches[index].Domain.Size)
				{
					color = Color.PeachPuff;
				}
				else if (Global.CheatList.IsActive(_watches.Domain, _watches[index].Address ?? 0))
				{
					color = Color.LightCyan;
				}
			}
		}

		private void WatchListView_QueryItemText(int index, int column, out string text)
		{
			text = string.Empty;

			if (index >= _watches.ItemCount || _watches[index].IsSeparator)
			{
				return;
			}

			var columnName = WatchListView.Columns[column].Name;

			switch (columnName)
			{
				case WatchList.ADDRESS:
					text = _watches[index].AddressString;
					break;
				case WatchList.VALUE:
					text = _watches[index].ValueString;
					break;
				case WatchList.PREV:
					text = _watches[index].PreviousStr;
					break;
				case WatchList.CHANGES:
					if (!_watches[index].IsSeparator)
					{
						text = _watches[index].ChangeCount.ToString();
					}

					break;
				case WatchList.DIFF:
					text = _watches[index].Diff;
					break;
				case WatchList.DOMAIN:
					text = _watches[index].Domain.Name;
					break;
				case WatchList.NOTES:
					text = _watches[index].Notes;
					break;
			}
		}

		private void RefreshFloatingWindowControl()
		{
			Owner = Settings.FloatingWindow ? null : GlobalWin.MainForm;
		}

		#endregion

		#region Winform Events

		#region File Menu

		private void FileSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			SaveMenuItem.Enabled = _watches.Changes;
		}

		private void NewListMenuItem_Click(object sender, EventArgs e)
		{
			NewWatchList(false);
		}

		private void OpenMenuItem_Click(object sender, EventArgs e)
		{
			var append = sender == AppendMenuItem;
			LoadWatchFile(ToolHelpers.GetWatchFileFromUser(_watches.CurrentFileName), append);
		}

		private void SaveMenuItem_Click(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(_watches.CurrentFileName))
			{
				if (_watches.Save())
				{
					UpdateStatusBar(saved: true);
				}
			}
			else
			{
				SaveAs();
			}
		}

		private void SaveAsMenuItem_Click(object sender, EventArgs e)
		{
			SaveAs();
		}

		private void RecentSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			RecentSubMenu.DropDownItems.Clear();
			RecentSubMenu.DropDownItems.AddRange(
				Global.Config.RecentWatches.RecentMenu(LoadFileFromRecent, true));
		}

		private void ExitMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		#endregion

		#region Watch

		private void WatchesSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			EditWatchMenuItem.Enabled =
				DuplicateWatchMenuItem.Enabled =
				RemoveWatchMenuItem.Enabled =
				MoveUpMenuItem.Enabled =
				MoveDownMenuItem.Enabled =
				PokeAddressMenuItem.Enabled =
				FreezeAddressMenuItem.Enabled =
				SelectedIndices.Any();

			PauseMenuItem.Text = _paused ? "Unpause" : "Pause";
		}

		private void MemoryDomainsSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			MemoryDomainsSubMenu.DropDownItems.Clear();
			MemoryDomainsSubMenu.DropDownItems.AddRange(
				_memoryDomains.MenuItems(SetMemoryDomain, _watches.Domain.Name)
				.ToArray());
		}

		private void NewWatchMenuItem_Click(object sender, EventArgs e)
		{
			var we = new WatchEditor
			{
				InitialLocation = this.ChildPointToScreen(WatchListView),
				MemoryDomains = _memoryDomains
			};
			we.SetWatch(_watches.Domain);
			we.ShowHawkDialog();
			if (we.DialogResult == DialogResult.OK)
			{
				_watches.Add(we.Watches[0]);
				Changes();
				UpdateWatchCount();
				WatchListView.ItemCount = _watches.ItemCount;
				UpdateValues();
			}
		}

		private void EditWatchMenuItem_Click(object sender, EventArgs e)
		{
			EditWatch();
		}

		private void RemoveWatchMenuItem_Click(object sender, EventArgs e)
		{
			var items = SelectedItems.ToList();
			if (items.Any())
			{
				foreach (var item in items)
				{
					_watches.Remove(item);
				}

				WatchListView.ItemCount = _watches.ItemCount;
				UpdateValues();
				UpdateWatchCount();
			}
		}

		private void DuplicateWatchMenuItem_Click(object sender, EventArgs e)
		{
			EditWatch(duplicate: true);
		}

		private void PokeAddressMenuItem_Click(object sender, EventArgs e)
		{
			if (SelectedWatches.Any())
			{
				var poke = new RamPoke
				{
					InitialLocation = this.ChildPointToScreen(WatchListView)
				};

				poke.SetWatch(SelectedWatches);

				if (poke.ShowHawkDialog() == DialogResult.OK)
				{
					UpdateValues();
				}
			}
		}

		private void FreezeAddressMenuItem_Click(object sender, EventArgs e)
		{
			var allCheats = SelectedWatches.All(x => Global.CheatList.IsActive(x.Domain, x.Address ?? 0));
			if (allCheats)
			{
				SelectedWatches.UnfreezeAll();
			}
			else
			{
				SelectedWatches.FreezeAll();
			}
		}

		private void InsertSeparatorMenuItem_Click(object sender, EventArgs e)
		{
			var indexes = SelectedIndices.ToList();
			if (indexes.Any())
			{
				_watches.Insert(indexes[0], SeparatorWatch.Instance);
			}
			else
			{
				_watches.Add(SeparatorWatch.Instance);
			}

			WatchListView.ItemCount = _watches.ItemCount;
			Changes();
			UpdateWatchCount();
		}

		private void ClearChangeCountsMenuItem_Click(object sender, EventArgs e)
		{
			_watches.ClearChangeCounts();
		}

		private void MoveUpMenuItem_Click(object sender, EventArgs e)
		{
			var indexes = SelectedIndices.ToList();
			if (!indexes.Any() || indexes[0] == 0)
			{
				return;
			}

			foreach (var index in indexes)
			{
				var watch = _watches[index];
				_watches.Remove(watch);
				_watches.Insert(index - 1, watch);
			}

			Changes();

			var indices = indexes.Select(t => t - 1).ToList();

			WatchListView.SelectedIndices.Clear();
			foreach (var t in indices)
			{
				WatchListView.SelectItem(t, true);
			}

			WatchListView.ItemCount = _watches.ItemCount;
		}

		private void MoveDownMenuItem_Click(object sender, EventArgs e)
		{
			var indices = SelectedIndices.ToList();
			if (indices.Count == 0 || indices.Last() == _watches.Count - 1)
			{
				return;
			}

			for (var i = indices.Count - 1; i >= 0; i--)
			{
				var watch = _watches[indices[i]];
				_watches.Remove(watch);
				_watches.Insert(indices[i] + 1, watch);
			}

			var newindices = indices.Select(t => t + 1).ToList();

			WatchListView.SelectedIndices.Clear();
			foreach (var t in newindices)
			{
				WatchListView.SelectItem(t, true);
			}

			Changes();
			WatchListView.ItemCount = _watches.ItemCount;
		}

		private void SelectAllMenuItem_Click(object sender, EventArgs e)
		{
			WatchListView.SelectAll();
		}

		private void PauseMenuItem_Click(object sender, EventArgs e)
		{
			_paused ^= true;
		}

		#endregion

		#region Options

		private void OptionsSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			WatchesOnScreenMenuItem.Checked = Global.Config.DisplayRamWatch;
			SaveWindowPositionMenuItem.Checked = Settings.SaveWindowPosition;
			AlwaysOnTopMenuItem.Checked = Settings.TopMost;
			FloatingWindowMenuItem.Checked = Settings.FloatingWindow;
		}

		private void DefinePreviousValueSubMenu_DropDownOpened(object sender, EventArgs e)
		{
			PreviousFrameMenuItem.Checked = Global.Config.RamWatchDefinePrevious == Watch.PreviousType.LastFrame;
			LastChangeMenuItem.Checked = Global.Config.RamWatchDefinePrevious == Watch.PreviousType.LastChange;
			OriginalMenuItem.Checked = Global.Config.RamWatchDefinePrevious == Watch.PreviousType.Original;
		}

		private void PreviousFrameMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.RamWatchDefinePrevious = Watch.PreviousType.LastFrame;
		}

		private void LastChangeMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.RamWatchDefinePrevious = Watch.PreviousType.LastChange;
		}

		private void OriginalMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.RamWatchDefinePrevious = Watch.PreviousType.Original;
		}

		private void WatchesOnScreenMenuItem_Click(object sender, EventArgs e)
		{
			Global.Config.DisplayRamWatch ^= true;

			if (!Global.Config.DisplayRamWatch)
			{
				GlobalWin.OSD.ClearGUIText();
			}
			else
			{
				UpdateValues();
			}
		}

		private void SaveWindowPositionMenuItem_Click(object sender, EventArgs e)
		{
			Settings.SaveWindowPosition ^= true;
		}

		private void AlwaysOnTopMenuItem_Click(object sender, EventArgs e)
		{
			TopMost = Settings.TopMost ^= true;
		}

		private void FloatingWindowMenuItem_Click(object sender, EventArgs e)
		{
			Settings.FloatingWindow ^= true;
			RefreshFloatingWindowControl();
		}

		private void RestoreDefaultsMenuItem_Click(object sender, EventArgs e)
		{
			Settings = new RamWatchSettings();
			Size = new Size(_defaultWidth, _defaultHeight);

			RamWatchMenu.Items.Remove(
				RamWatchMenu.Items
					.OfType<ToolStripMenuItem>()
					.First(x => x.Name == "GeneratedColumnsSubMenu")
			);

			RamWatchMenu.Items.Add(Settings.Columns.GenerateColumnsMenu(ColumnToggleCallback));

			Global.Config.DisplayRamWatch = false;

			RefreshFloatingWindowControl();
			LoadColumnInfo();
		}

		#endregion

		#region Dialog, Context Menu, and ListView Events

		private void NewRamWatch_Load(object sender, EventArgs e)
		{
			TopMost = Settings.TopMost;
			_watches = new WatchList(_memoryDomains, _memoryDomains.MainMemory, _emu.SystemId);
			LoadConfigSettings();
			RamWatchMenu.Items.Add(Settings.Columns.GenerateColumnsMenu(ColumnToggleCallback));
			UpdateStatusBar();
		}

		private void ColumnToggleCallback()
		{
			SaveColumnInfo();
			LoadColumnInfo();
		}

		private void NewRamWatch_Activated(object sender, EventArgs e)
		{
			WatchListView.Refresh();
		}

		private void NewRamWatch_DragEnter(object sender, DragEventArgs e)
		{
			e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
		}

		private void NewRamWatch_DragDrop(object sender, DragEventArgs e)
		{
			var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);
			if (Path.GetExtension(filePaths[0]) == ".wch")
			{
				_watches.Load(filePaths[0], append: false);
				WatchListView.ItemCount = _watches.ItemCount;
			}
		}

		private void NewRamWatch_Enter(object sender, EventArgs e)
		{
			WatchListView.Focus();
		}

		private void ListViewContextMenu_Opening(object sender, CancelEventArgs e)
		{
			var indexes = WatchListView.SelectedIndices;

			EditContextMenuItem.Visible =
				RemoveContextMenuItem.Visible =
				DuplicateContextMenuItem.Visible =
				PokeContextMenuItem.Visible =
				FreezeContextMenuItem.Visible =
				Separator6.Visible =
				InsertSeperatorContextMenuItem.Visible =
				MoveUpContextMenuItem.Visible =
				MoveDownContextMenuItem.Visible =
				Separator6.Visible = 
				indexes.Count > 0;

			var allCheats = _watches.All(x => Global.CheatList.IsActive(x.Domain, x.Address ?? 0));

			if (allCheats)
			{
				FreezeContextMenuItem.Text = "&Unfreeze Address";
				FreezeContextMenuItem.Image = Properties.Resources.Unfreeze;
			}
			else
			{
				FreezeContextMenuItem.Text = "&Freeze Address";
				FreezeContextMenuItem.Image = Properties.Resources.Freeze;
			}

			UnfreezeAllContextMenuItem.Visible = Global.CheatList.ActiveCount > 0;

			ViewInHexEditorContextMenuItem.Visible = SelectedWatches.Count() == 1;
		}

		private void UnfreezeAllContextMenuItem_Click(object sender, EventArgs e)
		{
			Global.CheatList.RemoveAll();
		}

		private void ViewInHexEditorContextMenuItem_Click(object sender, EventArgs e)
		{
			var selected = SelectedWatches.ToList();
			if (selected.Any())
			{
				GlobalWin.Tools.Load<HexEditor>();

				if (selected.Select(x => x.Domain).Distinct().Count() > 1)
				{
					ToolHelpers.ViewInHexEditor(selected[0].Domain, new List<int> { selected.First().Address ?? 0 }, selected.First().Size);
				}
				else
				{
					ToolHelpers.ViewInHexEditor(selected.First().Domain, selected.Select(x => x.Address ?? 0), selected.First().Size);
				}
			}
		}

		private void WatchListView_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete && !e.Control && !e.Alt && !e.Shift)
			{
				RemoveWatchMenuItem_Click(sender, e);
			}
			else if (e.KeyCode == Keys.C && e.Control && !e.Alt && !e.Shift) // Copy
			{
				CopyWatchesToClipBoard();
			}
			else if (e.KeyCode == Keys.Enter && !e.Control && !e.Alt && !e.Shift) // Enter
			{
				EditWatch();
			}
		}

		private void WatchListView_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			EditWatch();
		}

		private void WatchListView_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			OrderColumn(e.Column);
		}

		protected override void OnShown(EventArgs e)
		{
			RefreshFloatingWindowControl();
			base.OnShown(e);
		}

		private void ErrorIconButton_Click(object sender, EventArgs e)
		{
			var items = _watches
				.Where(watch => (watch.Address ?? 0) >= watch.Domain.Size)
				.ToList();

			foreach (var item in items)
			{
				_watches.Remove(item);
			}

			WatchListView.ItemCount = _watches.ItemCount;
			UpdateValues();
			UpdateWatchCount();
			UpdateStatusBar();
		}

		#endregion

		#endregion
	}
}
