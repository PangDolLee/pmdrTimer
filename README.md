# pmdrTimer — 포모도로 타이머

집중 시간, 짧은 휴식, 긴 휴식을 직접 설정하고 할 일까지 관리하는 포모도로 타이머입니다.

- 집중 / 짧은 휴식 / 긴 휴식 시간 설정, 몇 사이클 뒤 긴 휴식을 줄지 설정
- 간단한 플래너 (할 일 추가, 완료 체크, 삭제)
- "공부 세션 종료" 시 총 집중 시간 · 휴식 시간 · 완료 사이클 통계
- 종료 알림음 볼륨 설정 (집중 시작음 / 휴식 시작음은 서로 다름, 설정 화면에서 미리 듣기)
- 라이트(기본) / 다크 테마 전환 버튼 (선택은 저장됨)
- 집중 중에는 설정을 접고 그 자리에 미국 위인들의 명언을 표시
- exe 버전은 브라우저 없이 단독 실행되며, 설정·할 일·통계는 `%APPDATA%\PomodoroTimer`에만 저장되고 서버로 전송되지 않습니다
- 웹 버전(`docs/index.html`)은 브라우저(localStorage)에 저장됩니다

## 구성

| 경로 | 설명 |
|------|------|
| `docs/index.html` | 웹 배포용 단일 파일 (GitHub Pages가 이 폴더를 공개합니다) |
| `포모도로 타이머.exe` | Windows용 단일 실행 파일 (WinForms 네이티브 앱, 브라우저·추가 설치 불필요) |
| `src/` | exe 소스 (`Program.cs`, `Ui.cs`, `TimerView.cs`, `Sound.cs`, `Store.cs`, `Quotes.cs`)와 빌드 스크립트 `build.ps1` |

## Windows exe 다시 빌드

```powershell
powershell -ExecutionPolicy Bypass -File src\build.ps1
```

Windows에 기본 포함된 .NET Framework 컴파일러를 사용하므로 별도 설치가 필요 없습니다. 명언은 `src/Quotes.cs`에서 추가·수정합니다.
