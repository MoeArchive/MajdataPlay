using Cysharp.Threading.Tasks;
using MajdataPlay.Utils;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#nullable enable
namespace MajdataPlay
{
    public partial class SceneSwitcher : MonoBehaviour
    {
        Animator animator;
        public Image SubImage;
        public Image MainImage;
        private void Awake()
        {
            MajInstances.SceneSwitcher = this;
        }

        // Start is called before the first frame update
        void Start()
        {
            animator = GetComponent<Animator>();
            DontDestroyOnLoad(this);
        }

        public void SwitchScene(string sceneName)
        {
            SwitchSceneInternal(sceneName).Forget();
        }
        async UniTask SwitchSceneInternal(string sceneName)
        {
            SubImage.sprite = MajInstances.SkinManager.SelectedSkin.SubDisplay;
            MainImage.sprite = MajInstances.SkinManager.SelectedSkin.LoadingSplash;
            animator.SetBool("In", true);
            await UniTask.Delay(250);
            await SceneManager.LoadSceneAsync(sceneName);
            animator.SetBool("In", false);
        }
    }
    public partial class SceneSwitcher : MonoBehaviour
    {
        // Task
        async UniTask SwitchSceneInternalAsync(string sceneName, Task taskToRun)
        {
            SubImage.sprite = MajInstances.SkinManager.SelectedSkin.SubDisplay;
            MainImage.sprite = MajInstances.SkinManager.SelectedSkin.LoadingSplash;
            animator.SetBool("In", true);
            while (!taskToRun.IsCompleted)
                await UniTask.Yield();
            if (taskToRun.IsFaulted)
                Debug.LogException(taskToRun.Exception);
            await UniTask.Delay(300);
            await SceneManager.LoadSceneAsync(sceneName);
            animator.SetBool("In", false);
        }
        public async UniTaskVoid SwitchSceneAfterTaskAsync(string sceneName, Task taskToRun)
        {
            await SwitchSceneInternalAsync(sceneName, taskToRun);
        }
        // ValueTasl
        async UniTask SwitchSceneInternalAsync(string sceneName, ValueTask taskToRun)
        {
            SubImage.sprite = MajInstances.SkinManager.SelectedSkin.SubDisplay;
            MainImage.sprite = MajInstances.SkinManager.SelectedSkin.LoadingSplash;
            animator.SetBool("In", true);
            while (!taskToRun.IsCompleted)
                await UniTask.Yield();
            if(taskToRun.IsFaulted)
                Debug.LogException(taskToRun.AsTask().Exception);
            await UniTask.Delay(300);
            await SceneManager.LoadSceneAsync(sceneName);
            animator.SetBool("In", false);
        }
        public async UniTaskVoid SwitchSceneAfterTaskAsync(string sceneName, ValueTask taskToRun)
        {
            await SwitchSceneInternalAsync(sceneName, taskToRun);
        }
        // UniTask
        async UniTask SwitchSceneInternalAsync(string sceneName, UniTask taskToRun)
        {
            SubImage.sprite = MajInstances.SkinManager.SelectedSkin.SubDisplay;
            MainImage.sprite = MajInstances.SkinManager.SelectedSkin.LoadingSplash;
            animator.SetBool("In", true);
            while (taskToRun.Status is not (UniTaskStatus.Succeeded or UniTaskStatus.Faulted or UniTaskStatus.Canceled))
                await UniTask.Yield();
            if (taskToRun.Status is UniTaskStatus.Faulted)
                Debug.LogException(taskToRun.AsTask().Exception);
            await UniTask.Delay(300);
            await SceneManager.LoadSceneAsync(sceneName);
            animator.SetBool("In", false);
        }
        public async UniTaskVoid SwitchSceneAfterTaskAsync(string sceneName, UniTask taskToRun)
        {
            await SwitchSceneInternalAsync(sceneName, taskToRun);
        }


        // Task
        async UniTask<T> SwitchSceneInternalAsync<T>(string sceneName, Task<T> taskToRun)
        {
            SubImage.sprite = MajInstances.SkinManager.SelectedSkin.SubDisplay;
            MainImage.sprite = MajInstances.SkinManager.SelectedSkin.LoadingSplash;
            animator.SetBool("In", true);
            while (!taskToRun.IsCompleted)
                await UniTask.Yield();
            if (taskToRun.IsFaulted)
                throw taskToRun.Exception;
            await UniTask.Delay(300);
            await SceneManager.LoadSceneAsync(sceneName);
            return taskToRun.Result;
        }
        public async UniTask<T> SwitchSceneAfterTaskAsync<T>(string sceneName, Task<T> taskToRun)
        {
            return await SwitchSceneInternalAsync(sceneName, taskToRun);
        }
        // ValueTasl
        async UniTask<T> SwitchSceneInternalAsync<T>(string sceneName, ValueTask<T> taskToRun)
        {
            SubImage.sprite = MajInstances.SkinManager.SelectedSkin.SubDisplay;
            MainImage.sprite = MajInstances.SkinManager.SelectedSkin.LoadingSplash;
            animator.SetBool("In", true);
            while (!taskToRun.IsCompleted)
                await UniTask.Yield();
            if (taskToRun.IsFaulted)
                throw taskToRun.AsTask().Exception;
            await UniTask.Delay(300);
            await SceneManager.LoadSceneAsync(sceneName);
            animator.SetBool("In", false);
            return taskToRun.Result;
        }
        public async UniTask<T> SwitchSceneAfterTaskAsync<T>(string sceneName, ValueTask<T> taskToRun)
        {
            return await SwitchSceneInternalAsync(sceneName, taskToRun);
        }
        // UniTask
        async UniTask<T> SwitchSceneInternalAsync<T>(string sceneName, UniTask<T> taskToRun)
        {
            SubImage.sprite = MajInstances.SkinManager.SelectedSkin.SubDisplay;
            MainImage.sprite = MajInstances.SkinManager.SelectedSkin.LoadingSplash;
            animator.SetBool("In", true);
            while (taskToRun.Status is not (UniTaskStatus.Succeeded or UniTaskStatus.Faulted or UniTaskStatus.Canceled))
                await UniTask.Yield();
            await UniTask.Delay(300);
            await SceneManager.LoadSceneAsync(sceneName);
            animator.SetBool("In", false);
            switch(taskToRun.Status)
            {
                case UniTaskStatus.Succeeded:
                    return taskToRun.AsValueTask().Result;
                case UniTaskStatus.Faulted:
                    throw taskToRun.AsTask().Exception;
                default:
                    throw new TaskCanceledException();
            }
        }
        public async UniTask<T> SwitchSceneAfterTaskAsync<T>(string sceneName, UniTask<T> taskToRun)
        {
            return await SwitchSceneInternalAsync(sceneName, taskToRun);
        }
    }
}