﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.SceneManagement;

public class LevelParser : MonoBehaviour
{
    public static LevelParser Instance;

    public ScrollRect scrollView;
    public GameObject scrollContent;
    public GameObject scrollItemPrefab;
    public AudioSource audioSource;

    public GameObject infoContent;
    public GameObject songDetailPrefab;

    private List<Dictionary<string, string>> songs = new List<Dictionary<string, string>>();

    public Dictionary<string, string> selectedSong = new Dictionary<string, string>();

    static HashSet<string> metaInfo = new HashSet<string>
    {
        "AudioFilename",
        "PreviewTime",
        "Title",
        "Artist",
        "Creator",
        "Difficulty",
        "Source",
        "Tags",
        "LevelID",
        "HPDrainRate",
        "OverallDifficulty",
        "ApproachRate",

    };

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Instance = this;
    }

    // start is called before the first frame update
    void Start()
    {
        CreateSongList();
        foreach (Dictionary<string, string> song in songs)
        {
            GenerateSongButton(song);
        }

        scrollView.verticalNormalizedPosition = 1;
    }

    void GenerateSongInfo(Dictionary<string, string> song)
    {
        try
        {
            GameObject.Destroy(infoContent.transform.GetChild(0).gameObject);
        }
        catch
        {

        }
        
        GameObject songInfo = Instantiate(songDetailPrefab);
        songInfo.transform.SetParent(infoContent.transform,false);

        songInfo.transform.Find("CoverPhoto").gameObject.GetComponent<RawImage>().texture = Resources.Load<Texture>(song["CoverPhoto"]);
        songInfo.transform.Find("SongLength").gameObject.GetComponent<TextMeshProUGUI>().text = "Song Length: " + song["SongLength"];
        songInfo.transform.Find("MapLength").gameObject.GetComponent<TextMeshProUGUI>().text = "Map Length: " + song["MapLength"];
        songInfo.transform.Find("Bpm").gameObject.GetComponent<TextMeshProUGUI>().text = "Max BPM: " + song["MaxBpm"];
        songInfo.transform.Find("MemorySegments").gameObject.GetComponent<TextMeshProUGUI>().text = "Memory Segments: " + song["MemorySegments"];
        songInfo.transform.Find("HpDrain").gameObject.GetComponent<TextMeshProUGUI>().text = "HP Drain: " + song["HPDrainRate"];
        songInfo.transform.Find("OverallDifficulty").gameObject.GetComponent<TextMeshProUGUI>().text = "Overall Difficulty: " + song["OverallDifficulty"];
        songInfo.transform.Find("ApproachRate").gameObject.GetComponent<TextMeshProUGUI>().text = "Scroll Speed: " + song["ApproachRate"];

    }

    void GenerateSongButton(Dictionary<string, string> song)
    {
        GameObject songButton = Instantiate(scrollItemPrefab);

        songButton.transform.SetParent(scrollContent.transform, false);
        songButton.transform.Find("SongTitle").gameObject.GetComponent<TextMeshProUGUI>().text = song["Title"];
        songButton.transform.Find("ArtistCreator").gameObject.GetComponent<TextMeshProUGUI>().text = song["Artist"] + " // " + song["Creator"];

        songButton.GetComponent<Button>().onClick.AddListener(() => OnButtonClick(song));

    }

    void OnButtonClick(Dictionary<string, string> song)
    {
        audioSource.Stop();
        GenerateSongInfo(song);

        if (selectedSong.ContainsKey("AudioFilename") && selectedSong["AudioFilename"] == song["AudioFilename"])
        {
            SceneManager.LoadScene("Game");
        }
        else
        {
            audioSource.clip = Resources.Load<AudioClip>(song["SongPreview"]);
            audioSource.Play();
            selectedSong = song;
        }
        
    }

    void CreateSongList()
    {
        foreach (string directory in Directory.GetDirectories(".\\Assets\\Resources\\Songs"))
        {
            // Should only be one .memw file in each song directory
            string songMap = Directory.GetFiles(directory, "*.memw")[0];

            // Remove the file path for the mp3 and the extension
            string audioFile = Directory.GetFiles(directory, "*.mp3")[0].Replace(".\\Assets\\Resources\\", "").Replace(".mp3", "");
            int songLength = (int) Resources.Load<AudioClip>(audioFile).length;
            string coverFile = Directory.GetFiles(directory, "*.png")[0].Replace(".\\Assets\\Resources\\", "").Replace(".png", "").Replace(".jpeg", "");
            var songInfo = new Dictionary<string, string>
            {
                ["SongMap"] = songMap,
                ["Selected"] = "false",
                ["SongPreview"] = audioFile,
                ["SongLength"] = (songLength / 60).ToString() + ":" + (songLength % 60).ToString(),
                ["CoverPhoto"] = coverFile
            };

            StreamReader file = new StreamReader(songMap);
            string line, prevLine = "";
            string[] parts;
            bool readTimingPoints = false, readHitEvents = false;
            int maxBpm = 0, startOffset = 0, memSegs = 0;
            while ((line = file.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line[0] == '/')
                {
                    continue;
                }
                parts = line.Split(':');
                if (metaInfo.Contains(parts[0]))
                {
                    songInfo[parts[0]] = parts[1];
                }
                else if (line == "[TimingPoints]")
                {
                    readTimingPoints = true;
                }
                else if (line == "[HitEvents]")
                {
                    readTimingPoints = false;
                    readHitEvents = true;
                }
                else if (readTimingPoints)
                {
                    parts = line.Split(',');
                    Int32.TryParse(parts[1], out int bpm);
                    if (bpm > maxBpm)
                    {
                        maxBpm = bpm;
                    }
                    if (parts[4] == "1" || parts[4] == "-1")
                    {
                        memSegs++;
                    }
                }
                else if (readHitEvents)
                {
                    parts = line.Split(',');
                    Int32.TryParse(parts[1], out startOffset);
                    readHitEvents = false;
                }
                prevLine = line;

            }
            parts = prevLine.Split(',');
            Int32.TryParse(parts[1], out int endOffset);

            songInfo["MaxBpm"] = (maxBpm/1000f*60f).ToString();

            int mapLength = endOffset - startOffset;
            songInfo["MapLength"] = ((mapLength / 1000) / 60).ToString() + ":" + ((mapLength / 1000) % 60);

            songInfo["MemorySegments"] = memSegs.ToString();

            songs.Add(songInfo);
        }
    }

    public void ParseLevel(string filename)
    {
        // Read the file and display it line by line.  
        string line;
        bool timingPointsStart = false;
        bool hitEventsStart = false;
        string[] tmp;

        System.IO.StreamReader file = new System.IO.StreamReader(filename);
        while ((line = file.ReadLine()) != null)
        {
            System.Console.WriteLine(line);
            line = line.Trim();
            if (line.Length == 0 || line[0] == '/')
            {
                continue;
            }

            //Debug.Log(line);

            if (line == "[TimingPoints]")
            {
                //Debug.Log("TimingPoints start");
                hitEventsStart = false;
                timingPointsStart = true;
                continue;
            }
            if (line == "[HitEvents]")
            {
                //Debug.Log("hitobject start");
                hitEventsStart = true;
                timingPointsStart = false;
                continue;
            }

            if (hitEventsStart)
            {
                //Debug.Log(line);
                tmp = line.Split(',');
                HitEvent hitEvents = new HitEvent();
                hitEvents.setKey(tmp[0]);
                hitEvents.setOffset(tmp[1]);
                hitEvents.setIsNote(tmp[2]);
                hitEvents.setIsMine(tmp[2]);
                hitEvents.setColour(tmp[2]);
                hitEvents.setFlashBlack(tmp[2]);
                hitEvents.setIsHold(tmp[2]);
                if (tmp.Length < 5)
                {
                    if (tmp.Length == 4)
                        hitEvents.setColorArray(tmp[3]);
                }
                else
                {
                    hitEvents.setEndOffset(tmp[3]);
                    hitEvents.setColorArray(tmp[4]);
                }
                GameManager.Instance.hitEventsList.Add(hitEvents);
            }

            if (timingPointsStart)
            {
                tmp = line.Split(',');
                TimingPoints timingPoints = new TimingPoints(Convert.ToInt32(tmp[0]), Convert.ToSingle(tmp[1]), Convert.ToInt32(tmp[2]), Convert.ToInt32(tmp[3]), Convert.ToInt32(tmp[4]));
                GameManager.Instance.timingPointsList.Add(timingPoints);
            }
        }
        file.Close();
    }
}
