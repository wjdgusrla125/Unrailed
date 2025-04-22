
using System;
using System.Collections;
using Sound;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public enum GameState
{
    Lobby,
    InGame,
    Shop,
    GameOver,
}

public class GameManager: SingletonManager<GameManager>
{

    public TrainManager trainManager; //현재 게임의 기차
    public GameState CurrentGameState { get; set; } = GameState.Lobby;

    public AudioClip testClip;

    protected override void Awake()
    {
        base.Awake();
        SoundManager.Instance.PlayBGM(SoundManager.Instance.bgmClips[0], 0.5f);
    }

    private void Update()
    {
    }

    public void GameOver()
    {
        if (CurrentGameState != GameState.InGame) return;
        Debug.Log("게임오버");
        StartCoroutine(GameOverBGMCoroutine());
        if (!NetworkManager.Singleton.IsHost) return;
        RpcManager.Instance.ChangeGameStateRpc((int)GameState.GameOver);
        // if (trainManager) 
    }

    private IEnumerator GameOverBGMCoroutine()
    {
        float fadeDuration = 5.0f;
        SoundManager.Instance.FadeOutBGM(fadeDuration);
        SoundManager.Instance.StopSoundWithTag("Engine");
        yield return new WaitForSeconds(fadeDuration);
        SoundManager.Instance.PlayBGM(SoundManager.Instance.bgmClips[0], 0.5f);
    }
}
