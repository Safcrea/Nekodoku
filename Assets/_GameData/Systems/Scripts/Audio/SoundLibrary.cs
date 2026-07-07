using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundLibrary", menuName = "Picture Solitaire/Sound Library")]
public class SoundLibrary : ScriptableObject
{
    [Serializable]
    private class Entry
    {
        public SFX id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Serializable]
    private class MusicEntry
    {
        public BGM id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private List<Entry> _entries = new();
    [SerializeField] private List<MusicEntry> _musicEntries = new();

    private Dictionary<SFX, Entry> _lookup;
    private Dictionary<BGM, MusicEntry> _musicLookup;

    public bool TryGetClip(SFX id, out AudioClip clip, out float volume)
    {
        _lookup ??= BuildLookup();

        if (_lookup.TryGetValue(id, out Entry entry) && entry.clip != null)
        {
            clip = entry.clip;
            volume = entry.volume;
            return true;
        }

        clip = null;
        volume = 0f;
        return false;
    }

    public bool TryGetMusicClip(BGM id, out AudioClip clip, out float volume)
    {
        _musicLookup ??= BuildMusicLookup();

        if (_musicLookup.TryGetValue(id, out MusicEntry entry) && entry.clip != null)
        {
            clip = entry.clip;
            volume = entry.volume;
            return true;
        }

        clip = null;
        volume = 0f;
        return false;
    }

    private Dictionary<SFX, Entry> BuildLookup()
    {
        var map = new Dictionary<SFX, Entry>();
        foreach (Entry entry in _entries)
            map[entry.id] = entry;
        return map;
    }

    private Dictionary<BGM, MusicEntry> BuildMusicLookup()
    {
        var map = new Dictionary<BGM, MusicEntry>();
        foreach (MusicEntry entry in _musicEntries)
            map[entry.id] = entry;
        return map;
    }
}
