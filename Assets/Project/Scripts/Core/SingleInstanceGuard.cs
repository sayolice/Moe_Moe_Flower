#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// Windows 스탠드얼론 빌드에서 동일 실행 파일이 중복 실행되는 것을 막는다.
///
/// 왜 필요한가: 세이브 파일을 두 프로세스가 동시에 쓰면 저장이 서로 덮어써져 진행이 손실되거나
/// 파일이 손상된다. 오프라인 정산도 "마지막 저장 시각" 기준이라, 두 인스턴스가 각자 저장하면
/// 정산이 중복되거나 누락된다. 그래서 씬/게임 로직이 시작되기도 전(BeforeSplashScreen)에
/// 가장 먼저 판정해서, 세이브 시스템이 파일을 건드릴 기회 자체를 주지 않는다.
///
/// 왜 Named Mutex인가: 파일 락과 달리 OS 커널 오브젝트라서 프로세스가 살아있는 동안만 유효하다.
/// 비정상 종료(강제 종료/크래시)해도 프로세스가 끝나는 순간 OS가 그 프로세스의 모든 핸들을
/// 회수하므로, 이 뮤텍스도 함께 사라진다 — "죽은 인스턴스가 락을 영원히 들고 있어서 이후
/// 정상 실행까지 막히는" 상황 자체가 구조적으로 발생하지 않는다(별도 처리 불필요, OS 보장).
///
/// #if !UNITY_EDITOR 외에 UNITY_STANDALONE_WIN도 같이 건 이유: user32.dll P/Invoke가
/// Windows 전용이라, 다른 플랫폼 빌드(모바일 등)에서는 이 파일 자체가 통째로 빠져야
/// 컴파일이 깨지지 않는다. 에디터에서 안 도는 건 두 조건 다 지켜진다.
/// </summary>
public static class SingleInstanceGuard
{
    // 회사/제품명이 아니라 고정 GUID 기반 문자열을 쓴다 — 제품명이 나중에 바뀌거나 한글/공백이
    // 섞여도 Mutex 이름 유효성 문제가 없고, 다른 앱과의 이름 충돌 가능성도 원천 차단된다.
    private const string MutexName = "MoeMoeFlower_SingleInstance_9F1A6E22-6C3B-4E9E-8C7A-2D6E9B5E7F00";

    private static Mutex mutex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void CheckSingleInstance()
    {
        mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out bool createdNew);

        if (createdNew)
        {
            // 내가 최초(유일) 인스턴스 — 정상 종료 시 반드시 해제한다.
            Application.quitting += ReleaseMutex;
            return;
        }

        // 이미 실행 중인 인스턴스가 있다 — 이 뮤텍스의 소유권은 내가 가진 적이 없으므로
        // ReleaseMutex 없이 핸들만 닫는다(ReleaseMutex를 호출하면 소유하지 않은 뮤텍스라
        // SynchronizationLockException이 발생한다).
        mutex.Close();
        mutex = null;

        BringExistingWindowToFront();

        MessageBox(IntPtr.Zero,
            $"{Application.productName}이(가) 이미 실행 중입니다.",
            Application.productName,
            0x00000040 /* MB_ICONINFORMATION */);

        // 이 시점(BeforeSplashScreen)엔 씬/세이브 시스템이 전혀 시작되지 않았으므로 즉시 종료해도 안전하다.
        Application.Quit();
        Environment.Exit(0); // 빌드에서 Application.Quit()은 그 프레임 끝에 종료를 예약할 뿐이라, 확실한 즉시 종료를 위해 명시적으로 프로세스를 끝낸다.
    }

    private static void ReleaseMutex()
    {
        if (mutex == null) return;
        mutex.ReleaseMutex();
        mutex.Close();
        mutex = null;
    }

    /// <summary> 이미 떠 있는 기존 창을 찾아 포그라운드로 가져온다. 실패해도 치명적이지 않으므로 무시한다. </summary>
    private static void BringExistingWindowToFront()
    {
        IntPtr hWnd = FindWindow(null, Application.productName);
        if (hWnd == IntPtr.Zero) return;

        const int SW_RESTORE = 9;
        ShowWindow(hWnd, SW_RESTORE); // 최소화 상태였을 수 있으므로 복원부터
        SetForegroundWindow(hWnd);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
#endif
