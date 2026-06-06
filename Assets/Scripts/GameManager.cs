using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Levels")]
    [SerializeField] private List<LevelManager> levelPrefabs = new();

    [Header("UI")]
    [SerializeField] private Text levelText;
    [Header("Flow Timing")]
    [SerializeField] private float levelEndDelay = 1.5f;
    [Header("VFX")]
    public ParticleSystem winParticles;
    private LevelManager currentLevel;
    private int currentLevelIndex;
    private int levelsPlayed;
    private const string LEVEL_INDEX_KEY = "LEVEL_INDEX";
    private const string LEVELS_PLAYED_KEY = "LEVELS_PLAYED";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        LoadLevelIndex();
        LoadLevel(currentLevelIndex);
    }

    #region Level Flow

    private void LoadLevel(int index)
    {
        if (currentLevel != null)
        {

            Unsubscribe(currentLevel);
            Destroy(currentLevel.gameObject);
        }
        if (levelPrefabs.Count == 0)
        {
            Debug.LogError("No levels assigned!");
            return;
        }

        currentLevelIndex = Mathf.Clamp(index, 0, levelPrefabs.Count - 1);

        currentLevel = Instantiate(
            levelPrefabs[currentLevelIndex]
        );

        // Subscribe to level events
        currentLevel.onLevelWin += HandleLevelWin;
        currentLevel.onLevelFail += HandleLevelFail;

        UpdateLevelUI();
        SaveLevelIndex();
    }

    private void HandleLevelWin()
    {
        StartCoroutine(WinFlowRoutine());
    }
    private IEnumerator WinFlowRoutine()
    {
        winParticles.Play();
        // Wait for celebration (VFX, confetti, animations, etc.)
        yield return new WaitForSeconds(levelEndDelay);
        winParticles.Stop();
        // Move to next level
        NextLevel();
    }
    private void HandleLevelFail()
    {
        StartCoroutine(FailFlowRoutine());
    }
    private IEnumerator FailFlowRoutine()
    {
        yield return new WaitForSeconds(levelEndDelay); // small buffer for FX
        ReloadLevel();
    }
    #endregion

    #region Level Controls

    public void NextLevel()
    {
        levelsPlayed++;

        int nextIndex;

        if (levelsPlayed < levelPrefabs.Count)
        {
            // First full pass: sequential
            nextIndex = levelsPlayed;
        }
        else
        {
            // After finishing all levels: random
            nextIndex = GetRandomLevelIndex();
        }

        LoadLevel(nextIndex);
    }
    private int GetRandomLevelIndex()
    {
        if (levelPrefabs.Count <= 1)
            return 0;

        int randomIndex;

        do
        {
            randomIndex = Random.Range(0, levelPrefabs.Count);
        }
        while (randomIndex == currentLevelIndex);

        return randomIndex;
    }
    public void ReloadLevel()
    {
        LoadLevel(currentLevelIndex);
    }

    public void SkipLevel()
    {
        NextLevel();
    }

    #endregion

    #region UI

    private void UpdateLevelUI()
    {
        if (levelText != null)
        {
            levelText.text = "Level " + (currentLevelIndex + 1);
        }
    }

    #endregion

    #region Save / Load

    private void SaveLevelIndex()
    {
        PlayerPrefs.SetInt(LEVEL_INDEX_KEY, currentLevelIndex);
        PlayerPrefs.SetInt(LEVELS_PLAYED_KEY, levelsPlayed);
        PlayerPrefs.Save();
    }

    private void LoadLevelIndex()
    {
        currentLevelIndex = PlayerPrefs.GetInt(LEVEL_INDEX_KEY, 0);
        levelsPlayed = PlayerPrefs.GetInt(LEVELS_PLAYED_KEY, 0);
    }

    #endregion

    #region Button Hooks

    public void OnLevelReload()
    {
        ReloadLevel();
    }

    public void OnLevelSkip()
    {
        SkipLevel();
    }
    private void Unsubscribe(LevelManager level)
    {
        level.onLevelWin -= HandleLevelWin;
        level.onLevelFail -= HandleLevelFail;
    }
    #endregion
}