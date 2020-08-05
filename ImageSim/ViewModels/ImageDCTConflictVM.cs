using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ImageSim.Messages;
using System;

namespace ImageSim.ViewModels
{
    public class ImageDCTConflictVM : ConflictVM
    {
        private RelayCommand<ImageDetailsVM> keepImageCommand;
        private readonly Lazy<ImageDetailsVM> firstImage;
        private readonly Lazy<ImageDetailsVM> secondImage;
        private double similarityMetric;

        public double SimilarityMetric { get => similarityMetric; set => Set(ref similarityMetric, value); }
        public ImageDetailsVM FirstImage 
        { 
            get => firstImage.Value; 
        }
        public ImageDetailsVM SecondImage 
        { 
            get => secondImage.Value; 
        }
        public RelayCommand<ImageDetailsVM> KeepImageCommand => keepImageCommand
            ??= new RelayCommand<ImageDetailsVM>(HandleKeepImage);

        public ImageDCTConflictVM(string firstPath, string secondPath)
        {
            firstImage = new Lazy<ImageDetailsVM>(() => CreateDetails(firstPath));
            secondImage = new Lazy<ImageDetailsVM>(() => CreateDetails(secondPath));
        }

        private ImageDetailsVM CreateDetails(string path) => 
            (ImageDetailsVM)VMHelper.GetDetailsVMByPath(path);

        private void HandleKeepImage(ImageDetailsVM obj)
        {
            var toDelete = obj.FilePath == FirstImage.FilePath
                ? SecondImage.FilePath
                : FirstImage.FilePath;
            Messenger.Default.Send(new FileOperationMessage(toDelete, FileOperation.Delete));
        }
    }
}
