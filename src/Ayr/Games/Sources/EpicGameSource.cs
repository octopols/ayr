using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Ayr.Games.Sources
{
    /// <summary>The Epic Games Launcher keeps one JSON manifest per installed game under ProgramData.</summary>
    public sealed class EpicGameSource : IGameSource
    {
        private readonly DataContractJsonSerializer _serializer = new DataContractJsonSerializer(typeof(EpicManifest));

        public IEnumerable<GameEntry> Find()
        {
            string manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(manifests))
            {
                return Enumerable.Empty<GameEntry>();
            }
            return Directory.GetFiles(manifests, "*.item")
                .Select(Read)
                .Where(game => game != null && game.IsInstalled)
                .ToList();
        }

        private GameEntry Read(string file)
        {
            EpicManifest manifest;
            try
            {
                using (FileStream json = File.OpenRead(file))
                {
                    manifest = (EpicManifest)_serializer.ReadObject(json);
                }
            }
            catch (SerializationException)
            {
                return null;
            }
            return manifest.IsApplication ? GameEntry.TryCreate(manifest.DisplayName, manifest.InstallLocation) : null;
        }
    }
}
