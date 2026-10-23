using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RemofixZip.Models
{
    public class ZipTreeNode : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _fullPath = string.Empty;
        private bool _isDirectory;
        private bool _isExpanded = true;
        private bool _isSelected;
        private bool? _isChecked = false;
        private ZipEntryItem? _item;
        private ZipTreeNode? _parent;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string FullPath
        {
            get => _fullPath;
            set { _fullPath = value; OnPropertyChanged(); }
        }

        public bool IsDirectory
        {
            get => _isDirectory;
            set { _isDirectory = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public ZipTreeNode? Parent
        {
            get => _parent;
            set => _parent = value;
        }

        public ZipEntryItem? Item
        {
            get => _item;
            set { _item = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ZipTreeNode> Children { get; } = new();

        public bool? IsChecked
        {
            get => _isChecked;
            set => SetChecked(value, updateChildren: true, updateParent: true);
        }

        public void SetChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged(nameof(IsChecked));

            if (updateChildren && value.HasValue)
            {
                foreach (var child in Children)
                {
                    child.SetChecked(value.Value, updateChildren: true, updateParent: false);
                }
            }

            if (updateParent && Parent != null)
            {
                Parent.VerifyCheckState();
            }
        }

        private void VerifyCheckState()
        {
            bool? state = null;
            for (int i = 0; i < Children.Count; i++)
            {
                bool? current = Children[i].IsChecked;
                if (i == 0)
                {
                    state = current;
                }
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }

            SetChecked(state, updateChildren: false, updateParent: true);
        }

        public long CalculateTotalSize()
        {
            if (!IsDirectory) return Item?.Size ?? 0;
            return Children.Sum(c => c.CalculateTotalSize());
        }

        public int CalculateTotalFiles()
        {
            if (!IsDirectory) return 1;
            return Children.Sum(c => c.CalculateTotalFiles());
        }

        public string DisplaySize
        {
            get
            {
                if (!IsDirectory) return Item?.FormattedSize ?? "";
                long total = CalculateTotalSize();
                return total > 0 ? ZipEntryItem.FormatBytes(total) : "";
            }
        }

        public string SubtitleInfo
        {
            get
            {
                if (IsDirectory)
                {
                    int files = CalculateTotalFiles();
                    return files > 0 ? $"{files} dosya" : "Boş klasör";
                }
                else
                {
                    return Item?.FormattedSize ?? "";
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
