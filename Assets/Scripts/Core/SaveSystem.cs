using System;
using System.IO;
using UnityEngine;
using SaborColombiano.Data;

namespace SaborColombiano.Core
{
    /// <summary>
    /// Static utility class that handles persisting and restoring game state
    /// to/from disk using Unity's <see cref="JsonUtility"/> serialiser.
    /// <para>
    /// Save files are written to <see cref="Application.persistentDataPath"/>
    /// as JSON. The system maintains up to <see cref="MaxBackups"/> rotating
    /// backups so the player can recover from a corrupted write.
    /// </para>
    /// </summary>
    public static class SaveSystem
    {
        // ------------------------------------------------------------------ //
        //  Constants
        // ------------------------------------------------------------------ //

        /// <summary>Name of the primary save file (without path).</summary>
        private const string SaveFileName = "sabor_save.json";

        /// <summary>
        /// Maximum number of backup files kept on disk. Oldest backups are
        /// deleted when this limit is exceeded.
        /// </summary>
        public const int MaxBackups = 3;

        /// <summary>Extension appended to backup copies.</summary>
        private const string BackupExtension = ".bak";

        // ------------------------------------------------------------------ //
        //  Paths
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Full path to the primary save file.
        /// </summary>
        public static string SaveFilePath =>
            Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>
        /// Returns the full path for backup index <paramref name="index"/>
        /// (0 = most recent backup).
        /// </summary>
        private static string BackupPath(int index) =>
            Path.Combine(Application.persistentDataPath,
                         $"{Path.GetFileNameWithoutExtension(SaveFileName)}_{index}{BackupExtension}");

        // ------------------------------------------------------------------ //
        //  Save
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Serialises the supplied <see cref="GameData"/> to JSON and writes
        /// it to disk.  The previous save file is rotated into the backup
        /// chain before being overwritten.
        /// </summary>
        /// <param name="data">
        /// The game-state snapshot to persist. Must not be <c>null</c>.
        /// </param>
        /// <returns><c>true</c> if the write succeeded; <c>false</c> on error.</returns>
        public static bool SaveGame(GameData data)
        {
            if (data == null)
            {
                Debug.LogError("[SaveSystem] Cannot save null GameData.");
                return false;
            }

            try
            {
                // Stamp the save time.
                data.saveTimestamp = DateTime.UtcNow.ToString("o");
                data.version = GameData.CurrentVersion;

                string json = JsonUtility.ToJson(data, prettyPrint: true);

                // Rotate existing save into backups before overwriting.
                RotateBackups();

                // Write via a temp file to avoid half-written saves on crash.
                string tempPath = SaveFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(SaveFilePath))
                    File.Delete(SaveFilePath);
                File.Move(tempPath, SaveFilePath);

                Debug.Log($"[SaveSystem] Game saved to {SaveFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Save failed: {ex.Message}");
                return false;
            }
        }

        // ------------------------------------------------------------------ //
        //  Load
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Reads and deserialises the primary save file. If the primary file
        /// is missing or corrupt, the system attempts to load from the most
        /// recent backup.
        /// </summary>
        /// <returns>
        /// A populated <see cref="GameData"/> on success, or <c>null</c> if
        /// no valid save or backup could be loaded.
        /// </returns>
        public static GameData LoadGame()
        {
            // Try the primary save first.
            GameData data = TryLoadFromPath(SaveFilePath);
            if (data != null)
            {
                Debug.Log($"[SaveSystem] Loaded primary save (day {data.currentDay}).");
                return data;
            }

            // Fall back through backups, newest first.
            for (int i = 0; i < MaxBackups; i++)
            {
                string path = BackupPath(i);
                data = TryLoadFromPath(path);
                if (data != null)
                {
                    Debug.LogWarning($"[SaveSystem] Primary save missing/corrupt. " +
                                     $"Restored from backup {i}.");
                    return data;
                }
            }

            Debug.LogWarning("[SaveSystem] No valid save or backup found.");
            return null;
        }

        /// <summary>
        /// Attempts to read and deserialise a single file path.
        /// Returns <c>null</c> on any failure without throwing.
        /// </summary>
        private static GameData TryLoadFromPath(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                GameData data = JsonUtility.FromJson<GameData>(json);
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Failed to load {path}: {ex.Message}");
                return null;
            }
        }

        // ------------------------------------------------------------------ //
        //  Delete
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Deletes the primary save file and all backups.
        /// </summary>
        public static void DeleteSave()
        {
            TryDeleteFile(SaveFilePath);

            for (int i = 0; i < MaxBackups; i++)
                TryDeleteFile(BackupPath(i));

            Debug.Log("[SaveSystem] All save data deleted.");
        }

        // ------------------------------------------------------------------ //
        //  Query
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns <c>true</c> if a primary save file exists on disk.
        /// Does not validate the file contents.
        /// </summary>
        public static bool HasSave()
        {
            return File.Exists(SaveFilePath);
        }

        // ------------------------------------------------------------------ //
        //  Backup rotation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Rotates backup files so that the current primary save becomes
        /// backup 0, backup 0 becomes backup 1, and so on. The oldest
        /// backup beyond <see cref="MaxBackups"/> is deleted.
        /// </summary>
        private static void RotateBackups()
        {
            if (!File.Exists(SaveFilePath))
                return;

            // Shift existing backups down (oldest dropped).
            for (int i = MaxBackups - 1; i > 0; i--)
            {
                string older = BackupPath(i);
                string newer = BackupPath(i - 1);

                TryDeleteFile(older);

                if (File.Exists(newer))
                {
                    try
                    {
                        File.Move(newer, older);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SaveSystem] Backup rotation failed " +
                                         $"({newer} -> {older}): {ex.Message}");
                    }
                }
            }

            // Copy current save to backup slot 0.
            try
            {
                File.Copy(SaveFilePath, BackupPath(0), overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Failed to create backup 0: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a file if it exists, swallowing any exceptions.
        /// </summary>
        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Could not delete {path}: {ex.Message}");
            }
        }
    }
}
