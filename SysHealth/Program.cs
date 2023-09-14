using ImGuiNET;
using ClickableTransparentOverlay;
using System.Numerics;
using Veldrid.OpenGLBinding;
using System.Runtime.InteropServices;
using SharpDX.Direct3D11;
using System.Management;
using System.Diagnostics;
using System.Timers;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;

class Program : Overlay
{
    #region --------------[Win32 Functions]--------------

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    public enum SW : int
    {
        HIDE = 0,
        SHOW = 5
    }

    [DllImport("user32.dll", EntryPoint = "MessageBox")]
    public static extern int ShowMessage(int hWnd, string text, string caption, uint type);

    #endregion


    #region --------------[Menu Variables]--------------

    private bool showMenu = true;
    private Vector4 selectedColor = new Vector4(0.0f, 0.0f, 0.5f, 1.0f);

    #endregion


    #region --------------[CPU info]--------------

    private static List<string> cpuInfo = new List<string>();
    private static float cpuUsage;
    private const int MaxCpuDataPoints = 100;
    private List<float> cpuUsageHistory = new List<float>();

    #endregion


    #region --------------[Battery]--------------

    private static float battery_level;

    #endregion


    #region --------------[Stokage]--------------

    private static float total_stockage;
    private static float used_stockage;

    #endregion


    #region --------------[Memory]--------------

    private static float memoryAvaible;
    private const int MaxMemoryDataPoints = 1800;
    private List<float> memoryUsageHistory = new List<float>();

    #endregion


    #region --------------[Network Info]--------------

    private static int ping;
    private const int pingMaxDataPoints = 1500;
    private List<int> pingUsageHistory = new List<int>();
    private static List<string> networkInfo = new List<string>();
    private static string publicIP;

    #endregion


    protected override void Render()
    {
        if (showMenu)
        {
            ImGui.SetNextWindowSize(new Vector2(400, 390), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(100, 100), ImGuiCond.FirstUseEver);

            ImGuiStylePtr style = ImGui.GetStyle();
            style.WindowBorderSize = 2.9f;
            style.Colors[(int)ImGuiCol.Border] = selectedColor;
            style.Colors[(int)ImGuiCol.TitleBgActive] = selectedColor;


            ImGui.Begin("SysHealth", ref showMenu);

            ImGui.SeparatorText("Computer Info");
            ImGui.Text($"User: {Environment.UserName}");
            ImGui.Text($"Hostname: {networkInfo[0]}");

            ImGui.SeparatorText("CPU Info");
            ImGui.Text($"CPU Model: {cpuInfo[0]}");
            ImGui.Text($"Clock Speed: {cpuInfo[1]}");
            ImGui.Text($"Number of Cores: {cpuInfo[2]}");

            ImGui.SeparatorText("Battery");
            ImGui.ProgressBar(battery_level / 100.0f, new Vector2(0, 0), $"{battery_level:0.0}%");

            ImGui.SeparatorText("Stockage");
            ImGui.ProgressBar(used_stockage / total_stockage, new Vector2(0, 0), $"{used_stockage:0.0} Go");

            ImGui.SeparatorText("CPU Usage");
            ImGui.Text($"CPU Usage : {cpuUsage}%");

            cpuUsageHistory.Add(cpuUsage);
            if (cpuUsageHistory.Count > MaxCpuDataPoints)
            {
                cpuUsageHistory.RemoveAt(0);
            }

            float[] cpuUsageData = cpuUsageHistory.ToArray();
            ImGui.PlotLines("##CPU", ref cpuUsageData[0], cpuUsageData.Length, 0, null, 0, MaxCpuDataPoints, new Vector2(0, 80));

            ImGui.SeparatorText("Memory Avaible");
            memoryAvaible = GetMemoryAvaible();
            ImGui.Text($"Memory Available : {memoryAvaible} MB");

            memoryUsageHistory.Add(memoryAvaible);
            if (memoryUsageHistory.Count > MaxMemoryDataPoints)
            {
                memoryUsageHistory.RemoveAt(0);
            }

            float[] memoryUsageData = memoryUsageHistory.ToArray();
            ImGui.PlotLines("##Memory", ref memoryUsageData[0], memoryUsageData.Length, 0, null, 0, MaxMemoryDataPoints, new Vector2(0, 80));

            ImGui.SeparatorText("Network Info");
            ImGui.Text($"Router: {networkInfo[2]}");
            ImGui.Text($"Local IP: {networkInfo[1]}");
            ImGui.Text($"Public IP: {publicIP}");

            ImGui.Text($"Ping : {ping} ms");

            pingUsageHistory.Add(ping);
            if (pingUsageHistory.Count > MaxMemoryDataPoints)
            {
                pingUsageHistory.RemoveAt(0);
            }

            int[] pingUsageData = pingUsageHistory.ToArray();
            float[] pingUsageFloatData = Array.ConvertAll(pingUsageData, item => (float)item);

            ImGui.PlotLines("##Ping", ref pingUsageFloatData[0], pingUsageFloatData.Length, 0, null, 0, pingMaxDataPoints, new Vector2(0, 80));

            if (ImGui.Button("Refresh"))
            {
                publicIP = GetPublicIP();
                networkInfo = NetworkInfo();
            }

            ImGui.End();
        }
    }

    public static void Main(string[] args)
    {
        ShowWindow(GetConsoleWindow(), (int)SW.HIDE);

        publicIP = GetPublicIP();
        networkInfo = NetworkInfo();
        cpuInfo = GetCpuInfo();

        Program program = new Program();
        program.Start().Wait();

        Task.Run(() => MainMonitor());
    }

    private static List<string> GetCpuInfo()
    {
        List<string> cpuInfoList = new List<string>();

        var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
        var collection = searcher.Get();

        foreach (var obj in collection)
        {
            cpuInfoList.Add($"{obj["Name"]}");
            cpuInfoList.Add($"{obj["MaxClockSpeed"]} MHz");
            cpuInfoList.Add($"{obj["NumberOfCores"]}");
        }

        return cpuInfoList;
    }

    private static async Task<float> GetBatteryLevelAsync()
    {
        return await Task.Run(() =>
        {
            ObjectQuery query = new ObjectQuery("SELECT * FROM Win32_Battery");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection collection = searcher.Get();

            ManagementObject battery = collection.Cast<ManagementObject>().FirstOrDefault();

            if (battery != null)
            {
                return (float)Convert.ToDouble(battery["EstimatedChargeRemaining"]);
            }

            return 0.0f;
        });
    }

    private static double ConvertBytesToGB(ulong bytes)
    {
        return (bytes / 1024.0 / 1024.0 / 1024.0);
    }

    private static async Task GetStockageAsync()
    {
        string driveLetter = "C";

        await Task.Run(() =>
        {
            ObjectQuery query = new ObjectQuery($"SELECT * FROM Win32_LogicalDisk WHERE DeviceID='{driveLetter}:'");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection collection = searcher.Get();

            if (collection.Count == 1)
            {
                ManagementObject disk = collection.Cast<ManagementObject>().First();
                string volumeName = disk["VolumeName"].ToString();
                ulong totalSpaceBytes = Convert.ToUInt64(disk["Size"]);
                ulong freeSpaceBytes = Convert.ToUInt64(disk["FreeSpace"]);

                double totalSpaceGB = ConvertBytesToGB(totalSpaceBytes);
                double freeSpaceGB = ConvertBytesToGB(freeSpaceBytes);
                double usedSpaceGB = totalSpaceGB - freeSpaceGB;

                total_stockage = (float)totalSpaceGB;
                used_stockage = (float)usedSpaceGB;
            }
        });
    }

    private static float GetMemoryAvaible()
    {
        using (PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes"))
        {
            float availableMemory = ramCounter.NextValue();

            return availableMemory;
        }
    }

    private static async Task MainMonitor()
    {
        PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

        while (true)
        {
            cpuCounter.NextValue();
            Thread.Sleep(1000);
            cpuUsage = cpuCounter.NextValue();
            battery_level = await GetBatteryLevelAsync();
            await GetStockageAsync();
            ping = await GetPingTime(publicIP);

            Thread.Sleep(1000);
        }
    }

    private static List<string> NetworkInfo()
    {
        List<string> netinfo = new List<string>();

        try
        {
            string hostname = Dns.GetHostName();
            IPHostEntry hostEntry = Dns.GetHostEntry(hostname);

            netinfo.Add(hostname);
            netinfo.Add(hostEntry.AddressList[3].ToString());
            netinfo.Add(hostEntry.AddressList[2].ToString());
        }
        catch (Exception)
        {
            netinfo.Add("NO NETWORK DETECTED");
            netinfo.Add("NO NETWORK DETECTED");
        }

        return netinfo;
    }

    private static string GetPublicIP()
    {
        string IP = string.Empty;

        try
        {
            using (HttpClient client = new HttpClient())
            {
                var request = client.GetAsync("https://api.ipify.org/").Result;

                if (request.IsSuccessStatusCode)
                {
                    IP = request.Content.ReadAsStringAsync().Result;
                }

                return IP;
            }
        }
        catch (Exception)
        {
            return "WIFI REQUIRED";
        }
    }

    private static async Task<int> GetPingTime(string ipAddress)
    {
        try
        {
            if (ipAddress != "WIFI REQUIRED")
            {
                Ping pingSender = new Ping();
                PingReply reply = await pingSender.SendPingAsync(ipAddress);

                if (reply.Status == IPStatus.Success)
                {
                    return (int)reply.RoundtripTime;
                }
                else
                {
                    return -1;
                }
            }

            return -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }
}
