using MajdataPlay.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class SongLoader
{
    public static long TotalChartCount { get; private set; } = 0;
    public static ComponentState State { get; private set; } = ComponentState.Idle;
    public static List<SongDetail> ScanMusic()
    {
        if (!Directory.Exists(GameManager.ChartPath))
        {
            Directory.CreateDirectory(GameManager.ChartPath);
            return new List<SongDetail>();
        }

        List<SongDetail> songList = new List<SongDetail>();
        var path = GameManager.ChartPath;
        var dirs = new DirectoryInfo(path).GetDirectories();
        
        foreach (var dir in dirs)
        {
            var files = dir.GetFiles();
            var maidataFile = files.FirstOrDefault(o => o.Name is "maidata.txt");
            var trackFile = files.FirstOrDefault(o => o.Name is "track.mp3" or "track.ogg");
            var videoFile = files.FirstOrDefault(o => o.Name is "bg.mp4" or "pv.mp4" or "mv.mp4");
            var coverFile = files.FirstOrDefault(o => o.Name is "bg.png" or "bg.jpg");
            

            if (maidataFile is null || trackFile is null)
                continue;

            var song = new SongDetail();
            var txtcontent = File.ReadAllText(maidataFile.FullName);
            song = SongDetail.LoadFromMaidata(txtcontent);
            song.TrackPath = trackFile.FullName;
            song.Hash = GetHash(maidataFile.FullName, song.TrackPath);

            if (coverFile != null)
                song.SongCover = LoadSpriteFromFile(coverFile.FullName);
            if (videoFile != null)
                song.VideoPath = videoFile.FullName;

            songList.Add(song);
        }
        return songList;
    }
    public static string GetHash(string chartPath,string trackPath)
    {
        var hashComputer = SHA256.Create();
        using var chartStream = File.OpenRead(chartPath);
        using var trackStream = File.OpenRead(trackPath);
        var chartHash = hashComputer.ComputeHash(chartStream);
        var trackHash = hashComputer.ComputeHash(trackStream);

        byte[] raw = new byte[chartHash.Length + trackHash.Length];
        Buffer.BlockCopy(chartHash, 0, raw, 0, chartHash.Length);
        Buffer.BlockCopy(trackHash, 0, raw, chartHash.Length, trackHash.Length);

        var hash = hashComputer.ComputeHash(raw);

        return Convert.ToBase64String(hash);
    }
    static Sprite LoadSpriteFromFile(string FilePath)
    {
        Texture2D SpriteTexture = LoadTexture(FilePath);
        Sprite NewSprite = Sprite.Create(SpriteTexture, new Rect(0, 0, SpriteTexture.width, SpriteTexture.height), new Vector2(0.5f, 0.5f), 100.0f, 0, SpriteMeshType.FullRect);

        return NewSprite;
    }

    static Texture2D LoadTexture(string FilePath)
    {

        // Load a PNG or JPG file from disk to a Texture2D
        // Returns null if load fails

        Texture2D Tex2D;
        byte[] FileData;

        if (File.Exists(FilePath))
        {
            FileData = File.ReadAllBytes(FilePath);
            Tex2D = new Texture2D(2, 2);           // Create new "empty" texture
            if (Tex2D.LoadImage(FileData))           // Load the imagedata into the texture (size is set automatically)
                return Tex2D;                 // If data = readable -> return texture
        }
        return null;                     // Return null if load failed
    }
}
