﻿namespace hyperactive {
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows.Input;

    using LibGit2Sharp;

    using MoreLinq;

    public class ObjDbBranch : ViewModel, IBranch {
        private readonly Tree repoRoot;

        public string Name { get; }

        public bool IsHead { get; }

        private IDirectoryItem[] currentDirectory;
        public IDirectoryItem[] CurrentDirectory { get => currentDirectory; private set => SetProperty(ref currentDirectory, value); }

        private IDirectoryItem? selectedItem;
        public IDirectoryItem? SelectedItem {
            get => selectedItem;
            set {
                SetProperty(ref selectedItem, value);
                UpdateContent();
            }
        }

        private IFileContent? selectedContent;
        public IFileContent? SelectedContent { get => selectedContent; private set => SetProperty(ref selectedContent, value); }

        public ICommand NavigateCmd { get; }

        public ObjDbBranch(Branch branch) {
            repoRoot = branch.Tip.Tree;
            Name = branch.FriendlyName;
            IsHead = branch.IsCurrentRepositoryHead;
            CurrentDirectory = OpenRootFolder(repoRoot);
            NavigateCmd = new Command(Navigate);
        }

        private void UpdateContent() => SelectedContent = SelectedItem?.Type == ItemType.File
            ? SelectedItem.ToFileContent()
            : null;

        private void Navigate() {
            Debug.Assert(SelectedItem is not null);

            if (SelectedItem.Type == ItemType.Folder)
                CurrentDirectory = OpenFolder((ObjDbDirectoryItem)SelectedItem);
        }

        private static ObjDbDirectoryItem[] OpenRootFolder(Tree root)
            => OpenFolder(new ObjDbDirectoryItem(name: "(root)", gitObject: root, type: ItemType.Folder, parent: null));

        private static ObjDbDirectoryItem[] OpenFolder(ObjDbDirectoryItem folder) => folder
            .GitObject
            .Peel<Tree>()
            .OrderBy(item => item, Comparer<TreeEntry>.Create(DirectoriesFirst))
            .Select(item => new ObjDbDirectoryItem(item, parent: folder))
            .Insert(
                folder.IsRoot
                    ? Enumerable.Empty<ObjDbDirectoryItem>()
                    : new[] { new ObjDbDirectoryItem("[ .. ]", folder.Parent.GitObject, ItemType.Folder, folder.Parent.Parent) },
                index: 0)
            .ToArray();

        private static int DirectoriesFirst(TreeEntry a, TreeEntry b) => (a.Mode, b.Mode) switch {
            (Mode.Directory, Mode.Directory) => a.Name.CompareTo(b.Name),
            (Mode.Directory, _             ) => -1,
            (_             , Mode.Directory) => 1,
            (_             , _             ) => a.Name.CompareTo(b.Name)
        };
    }
}
