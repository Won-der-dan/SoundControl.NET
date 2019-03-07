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
using SoundControl.Models;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using System.Web.Mvc;
    // ReSharper disable StringLiteralTypo

namespace SoundControl.Controllers
{
    [System.Web.Http.Route("api/[controller]/[action]/{locationId?}/{trackId?}")]
    //[ApiController]
    public class SoundController : ApiController
    {
        private int _numberOfDevices;
        private static WaveOutEvent[] _waveOuts;
        private List<string> _list;
        private List<Track> tracks = new List<Track>();
        private static Random _rnd = new Random();
        private AppConfiguration _config;
        private const string ConfigPath = "C://Media//AppConfiguration.json";


        //Запускает поток для каждого WaveOutEvent
        //ID - номер устроиства в URL
        //trackid - что воспроизводить (необязательный)
        [System.Web.Http.HttpGet]
        public void Play(string locationId, string trackId)
        {
            try
            {
                AppConfiguration config = GetConfig();
                //Cписок имен звуковых выходов (кухня, спальня и т.д.)
                List<string> deviceNames = config.RoomNames;


                //Считаем количество именованных устроиств вывода в файле конфигурации
                try
                {
                    _numberOfDevices = config.RoomNames.Count();
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
                if (_waveOuts == null)
                {
                    _waveOuts = new WaveOutEvent[_numberOfDevices];
                }

                List<Device> devices = new List<Device>();
                using (FileStream fs = new FileStream("C://Media//devices.json", FileMode.Open))
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
                            if (cap.ManufacturerGuid.ToString() == device.ManufacturerGuid &&
                                cap.NameGuid.ToString() == device.NameGuid &&
                                cap.ProductGuid.ToString() == device.ProductGuid &&
                                cap.ProductName.ToString() == device.ProductName)
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
                    for (int i = 0; i < _numberOfDevices; i++)
                    {
                        Play(i.ToString(), trackId);
                    }
                else
                    //Запускаем поток

                    thread.Start();
            }
            #region exceptions
            catch (IndexOutOfRangeException)
            {
                //Если numberOfDevices равен нулю, то не удалось считать количество выходов из конфига, иначе неверный ввод
                if (_numberOfDevices == 0)
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


        [System.Web.Http.HttpGet]
        public void Stop(int locationId)
        {
            if (_waveOuts[locationId] != null && _waveOuts[locationId].PlaybackState == PlaybackState.Playing)
            {
                _waveOuts[locationId].Stop();
            }
        }

        public void Catalog()
        {
            String[] arr;
            _list = new List<String>();
            string pathToFiles = File.ReadLines(AppDomain.CurrentDomain.BaseDirectory + "conf.txt").ElementAtOrDefault(0);
            //считываем строку с директорией треков из конфига
            try
            {
                arr = Directory.GetFiles(pathToFiles, "*.mp3");
                for (int i = 0; i < arr.Length; i++)
                {
                    _list.Add(arr[i]);
                    tracks.Add(new Track() { Id = i + 1, Name = arr[i] });
                }
                //формируем каталог треков
            }
            #region exceptions
            catch
            {
                string message = "Directory " + pathToFiles + " not found";
                int code = 700;
                string type = "exсeption";
                Log(message, code, type);
                //обработка исключения, когда указан несуществующий путь к трекам
            }
            #endregion

        }

        [System.Web.Http.HttpGet]
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
            _config = GetConfig();

            //Если на данном устройстве что-то воспроизводится, останавливаем воспроизведение
            if (_waveOuts[location] != null)
            {
                _waveOuts[location].Stop();
            }
            //Получаем список треков
            Catalog();
            //Считываем путь к каталогу с звуками-событиями из файла конфигурации
            string eventCatalogue = _config.AlertPath;
            _waveOuts[location] = new WaveOutEvent
            {
                DeviceNumber = location
            };
            Mp3FileReader mp3Reader = null;
            //Если trackid не был указан в URL => выбираем случайно
            //Иначе музыкальный файл по номеру или алерт
            List<Alert> events = _config.Alerts;

            switch (track)
            {
                case null:
                    int trackid = _rnd.Next(_list.Count);
                    mp3Reader = new Mp3FileReader(_list[trackid]);
                    break;
                default:
                    try
                    {
                        foreach (Alert entry in _config.Alerts)
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
                            mp3Reader = new Mp3FileReader(_list[trackid - 1]);
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
                _waveOuts[location].Init(mp3Reader);
                _waveOuts[location].Play();
                _waveOuts[location].PlaybackStopped += new Disposer(mp3Reader).OnPlaybackStopped;

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
            string logpath = File.ReadLines(AppDomain.CurrentDomain.BaseDirectory + "conf.txt").ElementAtOrDefault(1);
            //считываем директорию для лога из конфига
            System.IO.File.AppendAllText(logpath +
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
            using (FileStream fs = new FileStream(ConfigPath, FileMode.Open))
            {
                DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(AppConfiguration));
                return (AppConfiguration)jsonFormatter.ReadObject(fs);
            }
        }
    }
}

//http://localhost:55525/api/Sound/play