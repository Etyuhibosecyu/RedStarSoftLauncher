using Avalonia.Media.Imaging;
using System.IO;

namespace RedStarSoftLauncher.ViewModels;

public class MainViewModel : ViewModelBase
{
	public Bitmap WindowBackground { get; } = new Bitmap(new MemoryStream(Properties.Resources.WindowBackground));
}
