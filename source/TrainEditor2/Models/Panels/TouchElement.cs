﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OpenBveApi.Interface;
using Prism.Mvvm;
using TrainEditor2.Extensions;
using TrainEditor2.Models.Others;

namespace TrainEditor2.Models.Panels
{
	internal class TouchElement : PanelElement
	{
		internal class SoundEntry : BindableBase, ICloneable
		{
			private int index;

			internal int Index
			{
				get
				{
					return index;
				}
				set
				{
					SetProperty(ref index, value);
				}
			}

			internal SoundEntry()
			{
				Index = -1;
			}

			public object Clone()
			{
				return MemberwiseClone();
			}
		}

		internal class CommandEntry : BindableBase, ICloneable
		{
			private Translations.CommandInfo info;
			private int option;

			internal Translations.CommandInfo Info
			{
				get
				{
					return info;
				}
				set
				{
					SetProperty(ref info, value);
				}
			}

			internal int Option
			{
				get
				{
					return option;
				}
				set
				{
					SetProperty(ref option, value);
				}
			}

			internal CommandEntry()
			{
				Info = Translations.CommandInfos.TryGetInfo(Translations.Command.None);
				Option = 0;
			}

			public object Clone()
			{
				return MemberwiseClone();
			}
		}

		private readonly CultureInfo culture;

		private double sizeX;
		private double sizeY;
		private int jumpScreen;

		private TreeViewItemModel treeItem;
		private TreeViewItemModel selectedTreeItem;

		private ListViewItemModel selectedListItem;

		internal ObservableCollection<SoundEntry> SoundEntries;
		internal ObservableCollection<CommandEntry> CommandEntries;

		internal ObservableCollection<ListViewColumnHeaderModel> ListColumns;
		internal ObservableCollection<ListViewItemModel> ListItems;

		internal double SizeX
		{
			get
			{
				return sizeX;
			}
			set
			{
				SetProperty(ref sizeX, value);
			}
		}

		internal double SizeY
		{
			get
			{
				return sizeY;
			}
			set
			{
				SetProperty(ref sizeY, value);
			}
		}

		internal int JumpScreen
		{
			get
			{
				return jumpScreen;
			}
			set
			{
				SetProperty(ref jumpScreen, value);
			}
		}

		internal TreeViewItemModel TreeItem
		{
			get
			{
				return treeItem;
			}
			set
			{
				SetProperty(ref treeItem, value);
			}
		}

		internal TreeViewItemModel SelectedTreeItem
		{
			get
			{
				return selectedTreeItem;
			}
			set
			{
				SetProperty(ref selectedTreeItem, value);
			}
		}

		internal ListViewItemModel SelectedListItem
		{
			get
			{
				return selectedListItem;
			}
			set
			{
				SetProperty(ref selectedListItem, value);
			}
		}

		internal TouchElement(Screen screen)
		{
			culture = CultureInfo.InvariantCulture;

			LocationX = 0.0;
			LocationY = 0.0;
			SizeX = 0.0;
			SizeY = 0.0;
			JumpScreen = screen.Number;
			SoundEntries = new ObservableCollection<SoundEntry>();
			CommandEntries = new ObservableCollection<CommandEntry>();
			Layer = 0;

			ListColumns = new ObservableCollection<ListViewColumnHeaderModel>();
			ListItems = new ObservableCollection<ListViewItemModel>();

			CreateTreeItem();
			SelectedTreeItem = TreeItem;
		}

		public override object Clone()
		{
			TouchElement touch = (TouchElement)MemberwiseClone();
			touch.SoundEntries = new ObservableCollection<SoundEntry>(SoundEntries.Select(x => (SoundEntry)x.Clone()));
			touch.CommandEntries = new ObservableCollection<CommandEntry>(CommandEntries.Select(x => (CommandEntry)x.Clone()));

			touch.ListColumns = new ObservableCollection<ListViewColumnHeaderModel>();
			touch.ListItems = new ObservableCollection<ListViewItemModel>();

			touch.CreateTreeItem();
			touch.SelectedTreeItem = touch.TreeItem;

			return touch;
		}

		internal void CreateTreeItem()
		{
			treeItem = new TreeViewItemModel(null) { Title = Utilities.GetInterfaceString("panel_settings", "sound_command", "tree", "touch_element") };
			treeItem.Children.Add(new TreeViewItemModel(TreeItem) { Title = Utilities.GetInterfaceString("panel_settings", "sound_command", "tree", "sounds") });
			treeItem.Children.Add(new TreeViewItemModel(TreeItem) { Title = Utilities.GetInterfaceString("panel_settings", "sound_command", "tree", "commands") });
			OnPropertyChanged(new PropertyChangedEventArgs(nameof(TreeItem)));
		}

		internal void CreateListColumns()
		{
			ListColumns.Clear();

			if (SelectedTreeItem == TreeItem.Children[0])
			{
				ListColumns.Add(new ListViewColumnHeaderModel { Text = "Index" });
			}

			if (SelectedTreeItem == TreeItem.Children[1])
			{
				ListColumns.Add(new ListViewColumnHeaderModel { Text = "Name" });
				ListColumns.Add(new ListViewColumnHeaderModel { Text = "Option" });
			}
		}

		internal void CreateListItems()
		{
			ListItems.Clear();

			if (SelectedTreeItem == TreeItem.Children[0])
			{
				foreach (SoundEntry entry in SoundEntries)
				{
					ListViewItemModel newItem = new ListViewItemModel { Texts = new ObservableCollection<string>(new string[1]), Tag = entry };
					UpdateListItem(newItem);
					ListItems.Add(newItem);
				}
			}

			if (SelectedTreeItem == TreeItem.Children[1])
			{
				foreach (CommandEntry entry in CommandEntries)
				{
					ListViewItemModel newItem = new ListViewItemModel { Texts = new ObservableCollection<string>(new string[2]), Tag = entry };
					UpdateListItem(newItem);
					ListItems.Add(newItem);
				}
			}
		}

		internal void UpdateListItem(ListViewItemModel item)
		{
			if (item.Tag is SoundEntry soundEntry)
			{
				item.Texts[0] = soundEntry.Index.ToString(culture);
			}
			else if (item.Tag is CommandEntry commandEntry)
			{
				item.Texts[0] = commandEntry.Info.Name;
				item.Texts[1] = commandEntry.Option.ToString(culture);
			}
		}

		internal void AddSoundEntry()
		{
			SoundEntry entry = new SoundEntry();

			SoundEntries.Add(entry);

			ListViewItemModel newItem = new ListViewItemModel { Texts = new ObservableCollection<string>(new string[1]), Tag = entry };
			UpdateListItem(newItem);
			ListItems.Add(newItem);

			SelectedListItem = ListItems.Last();
		}

		internal void AddCommandEntry()
		{
			CommandEntry entry = new CommandEntry();

			CommandEntries.Add(entry);

			ListViewItemModel newItem = new ListViewItemModel { Texts = new ObservableCollection<string>(new string[2]), Tag = entry };
			UpdateListItem(newItem);
			ListItems.Add(newItem);

			SelectedListItem = ListItems.Last();
		}

		internal void CopySoundEntry()
		{
			SoundEntry entry = (SoundEntry)((SoundEntry)SelectedListItem.Tag).Clone();

			SoundEntries.Add(entry);

			ListViewItemModel newItem = new ListViewItemModel { Texts = new ObservableCollection<string>(new string[1]), Tag = entry };
			UpdateListItem(newItem);
			ListItems.Add(newItem);

			SelectedListItem = ListItems.Last();
		}

		internal void CopyCommandEntry()
		{
			CommandEntry entry = (CommandEntry)((CommandEntry)SelectedListItem.Tag).Clone();

			CommandEntries.Add(entry);

			ListViewItemModel newItem = new ListViewItemModel { Texts = new ObservableCollection<string>(new string[2]), Tag = entry };
			UpdateListItem(newItem);
			ListItems.Add(newItem);

			SelectedListItem = ListItems.Last();
		}

		internal void RemoveSoundEntry()
		{
			SoundEntry entry = (SoundEntry)SelectedListItem.Tag;

			SoundEntries.Remove(entry);

			ListItems.Remove(SelectedListItem);

			SelectedListItem = null;
		}

		internal void RemoveCommandEntry()
		{
			CommandEntry entry = (CommandEntry)SelectedListItem.Tag;

			CommandEntries.Remove(entry);

			ListItems.Remove(SelectedListItem);

			SelectedListItem = null;
		}

		public override void WriteCfg(string fileName, StringBuilder builder)
		{
			throw new NotImplementedException();
		}

		public override void WriteXML(string fileName, XElement parent)
		{
			XElement touchNode = new XElement("Touch",
				new XElement("Location", $"{LocationX}, {LocationY}"),
				new XElement("Layer", Layer),
				new XElement("Size", $"{SizeX}, {SizeY}"),
				new XElement("JumpScreen", JumpScreen)
			);

			if (SoundEntries.Any())
			{
				touchNode.Add(new XElement("SoundEntries", SoundEntries.Select(WriteTouchElementSoundEntryNode)));
			}

			if (CommandEntries.Any())
			{
				touchNode.Add(new XElement("CommandEntries", CommandEntries.Select(WriteTouchElementCommandEntryNode)));
			}

			parent.Add(touchNode);
		}

		private XElement WriteTouchElementSoundEntryNode(SoundEntry entry)
		{
			return new XElement("Entry",
				new XElement("Index", entry.Index)
			);
		}

		private XElement WriteTouchElementCommandEntryNode(CommandEntry entry)
		{
			return new XElement("Entry",
				new XElement("Name", entry.Info.Name),
				new XElement("Option", entry.Option)
			);
		}
	}
}
