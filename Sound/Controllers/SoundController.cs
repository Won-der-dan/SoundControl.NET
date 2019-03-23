using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web.Http;
using System.IO;
using System.Configuration;
using System.Collections.Specialized;
using Sound.Models;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;

namespace WebApplication.Controllers
{

    public class SoundController : ApiController
    {
        public int numberOfDevices;
        public static WaveOutEvent[] waveOuts;
        public List<string> list;
        public List<Track> tracks = new List<Track>();
        private static Random rnd = new Random();
        private AppConfiguration config;
        private const string CONFIG_PATH = "C://Media//AppConfiguration.json";


        //Запускает поток для каждого WaveOutEvent
        //ID - номер устроиства в URL
        //trackid - что воспроизводить (необязательный)
        [HttpGet]
        public void Play(string locationId, string trackId)
        {
            try
            {
                AppConfiguration config = GetConfig();
                //список имен звуковых выходов (кухня, спальня и т.д.)
                List<string> deviceNames = config.RoomNames;


                //Считаем количество именованных устроиств вывода в файле конфигурации
                try
                {
                    numberOfDevices = config.RoomNames.Count();
                }
                #region exceptions
                //Exception появляется, если в файле конфигурации вместо ожидаемого числа устроиств считывается строка
                catch (FormatException)
                {
                    string wmessage = "Неверно сконфигурировано количество выходов в файле конфигурации.";
                    int wcode = 400;
                    string wtype = "error";
                    Log(wmessage, wcode, wtype);
                    //throw;
                }
                #endregion

                //хранит нужный deviceNumber, в то время как во входной переменной locationId могут быть введены 
                //названия комнат (kitchen, room1, etc.)
                int deviceNumber;


                Thread thread = null;

                //Если входной параметр ID не приводится к целочисленному типу, ищем его в коллекции и
                //присваиваем индекс найденного ID переменной location (хранит deviceNumber)
                if (!(Int32.TryParse(locationId, out deviceNumber)))
                {
                    if (deviceNames.Contains(locationId.ToLower())) { deviceNumber = deviceNames.IndexOf(locationId.ToLower()); }
                }
                //Если массива, хранящего WaveoutEvent-ы еще не существует (первый запуск), создаем его
                if (waveOuts == null)
                {
                    waveOuts = new WaveOutEvent[numberOfDevices];
                }

                List<Device> devices = new List<Device>();
                using (FileStream fs = new FileStream(config.DeviceConfigPath, FileMode.Open))
                {
                    DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(List<Device>));
                    devices = (List<Device>)jsonFormatter.ReadObject(fs);
                }

                int selectedDevice = -1;
                foreach (Device device in devices)
                {
                    if (device.Location == locationId)
                    {
                        for (int i = 0; i < WaveOut.DeviceCount; i++)
                        {
                            bool validProductName = false;
                            WaveOutCapabilities cap = WaveOut.GetCapabilities(i);
                            if (cap.ProductName.Length - cap.ProductName.IndexOf('(') > 3
                                && cap.ProductName.IndexOf('(') != -1)
                            {
                                validProductName = true;
                            }
                            if ((validProductName ?
                                 cap.ProductName.ToString()
                                                .Substring(cap.ProductName.ToString()
                                                .IndexOf('(') + 1, 3).TrimEnd('-', ' ') : "0") == device.ProductName.ToString())
                            {
                                selectedDevice = i;
                                break;
                            }
                            
                        }
                    }
                }
                //Инициализируем поток
                thread = new Thread(() => StartPlay(selectedDevice, trackId));

                //Если location=all, рекурсивно запускаем функцию для всех устройств
                if (locationId == "all")
                    for (int i = 0; i < numberOfDevices; i++)
                    {
                        Play(deviceNames[i], trackId);
                    }
                else
                    //Запускаем поток

                    thread.Start();
            }
            #region exceptions
            catch (IndexOutOfRangeException)
            {
                //Если numberOfDevices равен нулю, то не удалось считать количество выходов из конфига, иначе неверный ввод
                if (numberOfDevices == 0)
                {
                    string wmessage = "Неверно сконфигурировано количество выходов в файле конфигурации.";
                    int wcode = 400;
                    string wtype = "error";
                    Log(wmessage, wcode, wtype);
                    //throw;
                }
                else
                {
                    string wmessage = "Неверно указан DeviceNumber.";
                    int wcode = 700;
                    string wtype = "exception";
                    Log(wmessage, wcode, wtype);
                    //throw;
                }
            }
            #endregion

        }


        [HttpGet]
        public void Stop(string locationId)
        {
            config = GetConfig();
            List<Device> devices = new List<Device>();
            using (FileStream fs = new FileStream(config.DeviceConfigPath, FileMode.Open))
            {
                DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(List<Device>));
                devices = (List<Device>)jsonFormatter.ReadObject(fs);
            }

            int selectedDevice = -1;
            foreach (Device device in devices)
            {
                if (device.Location == locationId)
                {
                    for (int i = 0; i < WaveOut.DeviceCount; i++)
                    {
                        WaveOutCapabilities cap = WaveOut.GetCapabilities(i);
                        if (cap.ProductName.Length - cap.ProductName.IndexOf('(') > 3
                         && cap.ProductName.IndexOf('(') != -1
                         && cap.ProductName.ToString()
                                           .Substring(cap.ProductName.ToString().IndexOf('(') + 1, 3)
                                           .TrimEnd('-', ' ') == device.ProductName.ToString())
                        {
                            selectedDevice = i;
                            break;
                        }
                    }
                }
            }

            if (waveOuts[selectedDevice] != null && waveOuts[selectedDevice].PlaybackState == PlaybackState.Playing)
            {
                waveOuts[selectedDevice].Stop();
            }
        }

        public void Catalog()
        {
            config = GetConfig();
            String[] arr;
            list = new List<String>();

            //считываем строку с директорией треков из конфига
            try
            {
                arr = Directory.GetFiles(config.MediaPath, "*.mp3");
                for (int i = 0; i < arr.Length; i++)
                {
                    list.Add(arr[i]);
                    tracks.Add(new Track() { Id = i + 1, Name = arr[i] });
                }
                //формируем каталог треков
            }
            #region exceptions
            catch
            {
                string message = "Directory " + config.MediaPath + " not found";
                int code = 700;
                string type = "exсeption";
                Log(message, code, type);
                //обработка исключения, когда указан несуществующий путь к трекам
            }
            #endregion

        }

        [HttpGet]
        public IEnumerable<Track> GetAllTracks()
        {
            Catalog();
            return tracks;
        }

        //public IHttpActionResult GetTrack(int id)
        //{
        //    Catalog();
        //    var track = tracks.FirstOrDefault((p) => p.Id == id);
        //    if (track == null)
        //    {
        //        return NotFound();
        //    }
        //    else
        //    {
        //        return Ok(track);
        //    }


        //}


        public void StartPlay(int location, string track)
        {
            config = GetConfig();

            //Если на данном устройстве что-то воспроизводится, останавливаем воспроизведение
            if (waveOuts[location] != null)
            {
                waveOuts[location].Stop();
            }
            //Получаем список треков
            Catalog();
            //Считываем путь к каталогу с звуками-событиями из файла конфигурации
            string eventCatalogue = config.AlertPath;
            waveOuts[location] = new WaveOutEvent
            {
                DeviceNumber = location
            };
            Mp3FileReader mp3Reader = null;
            //Если trackid не был указан в URL => выбираем случайно
            //Иначе музыкальный файл по номеру или алерт
            List<Alert> events = config.Alerts;

            switch (track)
            {
                case null:
                    int trackid = rnd.Next(list.Count);
                    mp3Reader = new Mp3FileReader(list[trackid]);
                    break;
                default:
                    try
                    {
                        foreach (Alert entry in config.Alerts)
                        {
                            if (entry.Name == track)
                            {
                                mp3Reader = new Mp3FileReader(eventCatalogue + entry.FileName);
                                break;
                            }
                        }
                        if (mp3Reader == null)
                        {
                            trackid = Convert.ToInt32(track);
                            mp3Reader = new Mp3FileReader(list[trackid - 1]);
                        }
                    }
                    #region exceptions
                    catch (FormatException)
                    {
                        string wmessage = "Неверно указан номер трека.";
                        int wcode = 700;
                        string wtype = "exception";
                        Log(wmessage, wcode, wtype);
                        //throw;
                    }
                    #endregion
                    break;
            }
            if (mp3Reader != null)
            {
                waveOuts[location].Init(mp3Reader);
                waveOuts[location].Play();
                waveOuts[location].PlaybackStopped += new Disposer(mp3Reader).OnPlaybackStopped;

            }
        }



        public class Disposer
        {
            Mp3FileReader Mp3FileReader;

            public Disposer(Mp3FileReader mp3FileReader) { Mp3FileReader = mp3FileReader; }

            public void OnPlaybackStopped<StoppedEventArgs>(object sender, StoppedEventArgs e)
            {
                Mp3FileReader.Dispose();
                (sender as WaveOutEvent).Dispose();

            }
        }

        public void Log(string message, int code, string type)
        {
            config = GetConfig();
            //считываем директорию для лога из конфига
            System.IO.File.AppendAllText(config.LogPath +
            DateTime.Now.ToString("yyyyMMdd") + ".log",
            "{" + "  " + "\"date\": \"" + DateTime.Now.ToString("dd.MM.yyyy") + "\", "
            + "  " + "\"time\": \"" + DateTime.Now.ToString("HH:mm:ss") + "\", " +
            "  " + "\"code\": \"" + code + "\", " +
            "  " + "\"type\": \"" + type + "\", " +
            "  " + "\"description\": \"" + message + "\"}\r\n");
            //запись в формате json
        }

        public AppConfiguration GetConfig()
        {
            using (FileStream fs = new FileStream(CONFIG_PATH, FileMode.Open))
            {
                DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(AppConfiguration));
                return (AppConfiguration)jsonFormatter.ReadObject(fs);
            }
        }
    }
}