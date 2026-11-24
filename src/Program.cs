using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

internal static class AppEdition
{
#if DESKPLANT_TEST
    internal const string StorageName = "DeskPlant-Test";
    internal const string WindowTitle = "DeskPlant 测试版";
#else
    internal const string StorageName = "DeskPlant";
    internal const string WindowTitle = "DeskPlant 桌面盆栽";
#endif
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        bool firstInstance;
        using (var instance = new Mutex(true, @"Local\" + AppEdition.StorageName + ".Instance", out firstInstance))
        {
            if (!firstInstance)
            {
                DesktopNative.ShowExistingPlant();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppEdition.StorageName);
            using (var plant = new PlantWindow(new PlantState(Path.Combine(folder, "state.txt"))))
            {
                Application.Run(plant);
            }
        }
    }
}
