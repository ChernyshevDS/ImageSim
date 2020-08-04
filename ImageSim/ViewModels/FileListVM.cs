using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using ImageSim.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ImageSim.ViewModels
{
    public class FileListVM : ViewModelBase
    {
        private readonly IFileService FileService;
        private readonly ObservableCollection<string> Files = new ObservableCollection<string>();
        
        private RelayCommand<string> deleteFileCommand;
        private RelayCommand<string> excludeFileCommand;
        private string selectedFile;
        private ViewModelBase fileDetailsVM = new EmptyDetailsVM();

        public ReadOnlyObservableCollection<string> LocatedFiles { get; }

        public RelayCommand<string> DeleteFileCommand => deleteFileCommand ??= new RelayCommand<string>(DeleteFile);
        public RelayCommand<string> ExcludeFileCommand => excludeFileCommand ??= new RelayCommand<string>(ExcludeFile);

        public string SelectedFile
        {
            get => selectedFile;
            set
            {
                if (Set(ref selectedFile, value))
                {
                    Messenger.Default.Send(new CurrentFileChangedMessage(value));
                }
            }
        }

        public ViewModelBase FileDetailsVM { get => fileDetailsVM; set => Set(ref fileDetailsVM, value); }

        public FileListVM(IFileService files)
        {
            this.FileService = files;
            LocatedFiles = new ReadOnlyObservableCollection<string>(Files);

            if (IsInDesignMode)
            {
                for (int i = 0; i < 10; i++)
                {
                    Files.Add("File " + (i + 1));
                }
                return;
            }

            Messenger.Default.Register<CurrentFileChangedMessage>(this, x =>
            {
                FileDetailsVM = VMHelper.GetDetailsVMByPath(x?.File);
            }, true);

            Messenger.Default.Register<FileOperationMessage>(this, msg =>
            {
                switch (msg.Action)
                {
                    case FileOperation.Exclude:
                        ExcludeFile(msg.FilePath);
                        break;
                    case FileOperation.Delete:
                        DeleteFile(msg.FilePath);
                        break;
                    default:
                        break;
                }
            });
        }

        private void ExcludeFile(string path)
        {
            var idx = LocatedFiles.IndexOf(path);
            Files.Remove(path);
            idx = idx.Clamp(0, LocatedFiles.Count - 1);
            if(idx < LocatedFiles.Count)
                SelectedFile = LocatedFiles[idx];

            Messenger.Default.Send(new FileRemovedMessage(path));
        }

        private void DeleteFile(string obj)
        {
            ExcludeFile(obj);
            try
            {
                FileService.DeleteFileToBin(obj);
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public bool AddFile(string path)
        {
            if (Files.Contains(path))
                return false;
            Files.Add(path);
            return true;
        }

        public void Clear()
        {
            this.Files.Clear();
            Messenger.Default.Send(new FileListResetMessage());
        }
    }
}
