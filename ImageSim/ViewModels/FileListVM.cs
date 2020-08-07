using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Collections;
using ImageSim.Messages;
using ImageSim.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ImageSim.ViewModels
{
    public class FileListVM : ViewModelBase
    {
        private readonly IFileService FileService;
        private readonly SortedObservableCollection<string> Files = new SortedObservableCollection<string>();
        
        private RelayCommand<string> deleteFileCommand;
        private RelayCommand<string> excludeFileCommand;
        private string selectedFile;
        private ViewModelBase fileDetailsVM = new EmptyDetailsVM();

        public ReadOnlySortedObservableCollection<string> LocatedFiles { get; }

        public RelayCommand<string> DeletePathCommand => deleteFileCommand ??= new RelayCommand<string>(HandleDeletePath);
        public RelayCommand<string> ExcludePathCommand => excludeFileCommand ??= new RelayCommand<string>(HandleExcludePath);

        public string SelectedFile
        {
            get => selectedFile;
            set
            {
                var old = selectedFile;
                if (Set(ref selectedFile, value))
                {
                    Messenger.Default.Send(new CurrentFileChangedMessage(old, value));
                    FileDetailsVM = VMHelper.GetDetailsVMByPath(value);
                }
            }
        }

        public ViewModelBase FileDetailsVM { get => fileDetailsVM; set => Set(ref fileDetailsVM, value); }

        public FileListVM(IFileService files)
        {
            this.FileService = files;
            LocatedFiles = new ReadOnlySortedObservableCollection<string>(Files);

            if (IsInDesignMode)
            {
                Files.Add("C:\\Workspace\\Images\\1.jpg");
                Files.Add("C:\\Workspace\\Images\\2.jpg");
                Files.Add("C:\\Workspace\\Images\\3.jpg");
                Files.Add("C:\\Workspace\\smth.txt");
                Files.Add("C:\\Yolo\\yolo.txt");
                Files.Add("D:\\blabla.txt");
                return;
            }

            Messenger.Default.Register<SetCurrentFileMessage>(this, x =>
            {
                if (SelectedFile != x.File)
                    SelectedFile = x.File;
            }, true);
        }

        private void HandleExcludePath(string path)
        {
            var candidates = LocatedFiles.Where(x => x.StartsWith(path)).ToList();
            if (candidates.Count != 0)
                Messenger.Default.Send(new FileOperationMessage(candidates, FileOperation.Exclude));
        }

        private void HandleDeletePath(string path)
        {
            var candidates = LocatedFiles.Where(x => x.StartsWith(path)).ToList();
            if (candidates.Count != 0)
                Messenger.Default.Send(new FileOperationMessage(candidates, FileOperation.Delete));
        }

        public void ExcludeFile(string path)
        {
            if (SelectedFile == path)
                SelectedFile = null;
            Files.Remove(path);

            Messenger.Default.Send(new FileRemovedMessage(path));
        }

        public void DeleteFile(string obj)
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

        public async Task<bool> AddFileAsync(string path)
        {
            var canAdd = await Task.Run(() => !Files.Contains(path));
            if (canAdd)
                Files.Add(path);
            return canAdd;
        }

        public void Clear()
        {
            this.Files.Clear();
            Messenger.Default.Send(new FileListResetMessage());
        }
    }
}
