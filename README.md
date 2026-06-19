# MarkFlow

[한국어](README.md) | [English](README.en.md)

**Windows를 위한 심플한 Markdown 에디터**

MarkFlow는 Windows 10/11 환경을 대상으로 하는 .NET 9 WPF 기반 Markdown 문서 뷰어 겸 편집기입니다. WebView 중심 렌더링 대신 Markdig와 WPF `FlowDocument`를 사용해 가볍게 동작하도록 구성했으며, Windows 네이티브 UI 감성과 빠른 문서 전환을 목표로 합니다.

## 핵심 기능

- Markdown 파일 열기, 최근 파일 목록, 창 전체 드래그 앤 드롭
- 제목, 본문, 코드 블록, 인라인 코드, 표, 인용문, 체크박스, 이미지, 링크, 수평선 렌더링
- 좌측 라이브러리/목차 패널과 접기 애니메이션
- 사이드바와 문서 헤더를 숨기는 풀 컴팩트 보기 모드 (`Ctrl+Shift+F`)
- 중앙 문서 뷰어, Markdown 편집기, 저장 전 미리보기
- Markdown 원문 편집 모드와 미리보며 편집하는 분할 모드
- 저장되지 않은 변경사항 오버레이 표시
- Light / Dark / System 테마
- 한국어 / English / 日本語 앱 언어 전환 및 선택값 저장
- 문서 타이포그래피 프리셋과 Google Fonts 기반 한글 폰트 캐싱
- Windows legacy acrylic blur 기반 배경 효과
- 커스텀 타이틀바, 라운딩 윈도우, 커스텀 캡션 버튼
- 파일 변경 감지와 debounce 재렌더링

## 기술 스택

- .NET 9 / WPF
- C# nullable enabled
- MVVM 스타일 구조
- Markdig `0.38.0`
- WPF `FlowDocument`
- Windows DWM / `SetWindowCompositionAttribute`
- `FileSystemWatcher` + `DispatcherTimer` debounce

## 프로젝트 구조

```text
MDViewer/
├─ Assets/                  # MarkFlow 로고, PNG, ICO 리소스
├─ Models/                  # 화면/서비스 간 전달 모델과 enum
├─ Resources/               # 공통 스타일, Light/Dark 테마 리소스
├─ Services/                # 렌더링, 테마, DWM, 폰트, 최근 파일, 감시 서비스
├─ Utilities/               # MVVM 커맨드와 ObservableObject
├─ ViewModels/              # MainViewModel
├─ App.xaml                 # 앱 리소스 병합
├─ MainWindow.xaml          # 메인 UI
├─ SettingsWindow.xaml      # 설정 창
├─ MDViewer.csproj          # WPF 프로젝트 파일
└─ MDViewer.sln             # Visual Studio 솔루션
```

## 주요 구성 요소

### MainWindow

앱의 메인 셸입니다. 커스텀 타이틀바, 좌측 라이브러리/목차, 중앙 문서 뷰어, 편집 도구, 드롭 오버레이를 구성합니다.

주요 처리:

- 창 전체 드래그 앤 드롭 이벤트
- 제목/목차 클릭 시 문서 위치 이동
- 마우스 휠 스크롤 감도 조정
- 테마 전환 시 페이드 연출
- 편집 도구 버튼의 Markdown 문법 삽입
- 최소화/최대화/닫기 버튼 처리

### MainViewModel

문서 상태와 UI 상태를 관리하는 중심 ViewModel입니다.

주요 상태:

- 현재 파일 경로와 문서 제목
- 렌더링된 `FlowDocument`
- 편집 중 Markdown 원문
- 저장 여부 상태
- 최근 파일 목록
- 목차 목록
- 사이드바 접힘 상태
- 편집 방식 선택 상태
- 테마, 타이포그래피, 문서 폰트 선택 상태

주요 명령:

- `OpenFileCommand`
- `ReloadCommand`
- `SaveCommand`
- `ToggleEditCommand`
- `ToggleSidebarCommand`
- `ToggleThemeCommand`

### MarkdownRendererService

Markdig로 Markdown을 파싱하고 WPF `FlowDocument`로 변환합니다. 파일 기반 렌더링은 수정 시간, 파일 크기, 테마, 타이포그래피, 폰트 정보를 포함한 캐시 키로 관리합니다.

지원 구조:

- Heading -> `Paragraph` + 목차 항목 생성
- Paragraph / Inline -> WPF `Run`, `Hyperlink`, `Image`
- Quote -> `Section`
- Code block -> 고정폭 폰트 블록
- List / Task list
- Pipe table
- Horizontal rule

### ThemeService

Light, Dark, System 테마를 적용하고 문서 타이포그래피 값을 제공합니다. 테마는 `Resources/Theme.Light.xaml`, `Resources/Theme.Dark.xaml` 리소스 딕셔너리를 교체하는 방식으로 적용합니다.

### LanguageService

한국어, 영어, 일본어 문자열 리소스 사전을 런타임에 교체하고 현재 문화권을 함께 적용합니다. 선택한 언어는 `%APPDATA%\MarkFlow\settings.json`에 저장되어 다음 실행에도 유지됩니다.

### AppSettingsService

테마, 타이포그래피, 본문 폰트, 편집 방식, 파일 변경 감지와 언어 선택을 `%APPDATA%\MarkFlow\settings.json`에 통합 저장합니다. 임시 파일 작성 후 교체하는 방식으로 저장하며 다음 실행 시 값을 복원합니다.

### DwmBackdropService

윈도우 배경 효과와 캡션 영역 처리를 담당합니다. 현재는 Mica가 아니라 Windows 10 legacy acrylic blur 효과를 우선 사용하도록 구성되어 있습니다.

처리 내용:

- 투명 클라이언트 영역 준비
- `SetWindowCompositionAttribute` 기반 acrylic blur 적용
- Windows 11 modern backdrop 비활성화
- 다크 캡션 속성 적용
- 최대화 상태에 따른 라운딩 코너 적용/해제

### FontCacheService

선택한 Google Fonts 한글 폰트를 처음 사용할 때 다운로드하여 `%APPDATA%\MDViewer\Fonts`에 캐싱합니다. 다운로드 후 private font resource로 등록하고 WPF `FontFamily`로 문서 렌더링에 적용합니다.

### FileWatcherService

현재 열린 파일의 외부 변경을 감지합니다. `FileSystemWatcher` 이벤트는 여러 번 발생할 수 있으므로 `DispatcherTimer`로 debounce 후 한 번만 갱신합니다.

### TweenService

사이드바 접기/펼치기 등 짧은 UI 전환 애니메이션을 담당합니다. `CompositionTarget.Rendering`을 남용하지 않고 필요한 값 변화만 tween합니다.

## 작동 로직

### 파일 열기

1. 사용자가 파일 열기, 최근 파일, 드래그 앤 드롭 중 하나로 Markdown 파일을 선택합니다.
2. `MainViewModel.LoadFileAsync`가 파일을 읽습니다.
3. `MarkdownRendererService.RenderFileAsync`가 Markdown을 `FlowDocument`로 변환합니다.
4. 문서 본문, 목차, 문서 정보, 최근 파일 목록이 갱신됩니다.
5. `FileWatcherService`가 해당 파일을 감시합니다.

### 편집과 미리보기

1. 보기/편집 버튼으로 편집 상태를 전환합니다.
2. Markdown 모드에서는 원문 편집기를 전체 폭으로 사용합니다.
3. 미리보며 편집 모드에서는 좌측에 원문, 우측에 렌더링 미리보기를 표시합니다.
4. 편집 중 라이브 프리뷰는 280ms debounce 후 갱신됩니다.
5. 저장하지 않고 보기 탭으로 돌아가도 현재 편집 내용으로 문서 뷰어가 갱신됩니다.
6. 저장 전 상태는 문서 영역 오버레이와 상태 라벨로 표시됩니다.

### 저장

1. `SaveCommand`가 현재 `MarkdownText`를 원본 파일에 씁니다.
2. 파일 감시는 저장 중 잠시 중지됩니다.
3. 저장 후 파일을 다시 로드하여 캐시, 문서 정보, 목차를 최신화합니다.

### 테마와 폰트

1. 테마 버튼 또는 설정 창에서 테마를 변경합니다.
2. `ThemeService`가 테마 리소스를 교체합니다.
3. 문서가 열려 있으면 현재 상태 기준으로 다시 렌더링합니다.
4. 폰트 선택 시 `FontCacheService`가 폰트를 캐싱하고 `ThemeService`에 적용합니다.

## 개발 환경

필수 항목:

- Windows 10 이상
- Visual Studio 2022 최신 버전 또는 .NET SDK 9 이상
- .NET Desktop Development 워크로드

권장 실행 방식:

1. Visual Studio에서 `MDViewer.sln`을 엽니다.
2. 시작 프로젝트가 `MDViewer`인지 확인합니다.
3. `Debug` 또는 `Release` 구성으로 빌드합니다.
4. 실행 파일명은 `MarkFlow.exe`로 생성됩니다.

CLI 빌드:

```powershell
dotnet restore
dotnet build -c Debug
```

Release 빌드:

```powershell
dotnet build -c Release
```

## 릴리즈 빌드

프레임워크 의존 배포:

```powershell
dotnet publish .\MDViewer.csproj -c Release -r win-x64 --self-contained false -o .\publish\win-x64
```

단일 파일 self-contained 배포:

```powershell
dotnet publish .\MDViewer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64-single
```

릴리즈 산출물 확인 항목:

- `MarkFlow.exe` 실행 여부
- 앱 아이콘과 태스크바 아이콘 표시
- acrylic blur 배경 적용 여부
- 파일 열기/드롭/최근 파일 동작
- 편집 후 저장 전 미리보기 동작
- 저장 후 문서 정보와 목차 갱신
- Light/Dark/System 테마 전환
- 한글 폰트 다운로드 및 캐싱

## 업데이트 구조

현재 프로젝트에는 자동 업데이트 모듈이 포함되어 있지 않습니다. 업데이트 배포는 GitHub Releases를 기준으로 다음 흐름을 권장합니다.

1. `main` 브랜치에 기능 작업을 병합합니다.
2. `dotnet build -c Release`로 기본 빌드를 확인합니다.
3. `dotnet publish`로 배포 폴더를 생성합니다.
4. 실행 확인 후 산출물을 zip으로 묶습니다.
5. GitHub Releases에 버전 태그와 함께 업로드합니다.

향후 자동 업데이트를 추가한다면 다음 구조가 적합합니다.

- `UpdateService`: GitHub Releases API 조회
- `VersionInfo` 모델: 최신 버전, 다운로드 URL, 릴리즈 노트
- `SettingsWindow`: 업데이트 확인 버튼과 현재 버전 표시
- 앱 시작 시 자동 확인 옵션
- 다운로드 후 외부 updater 프로세스로 교체 실행

초기 단계에서는 앱 본체와 updater를 과도하게 결합하지 않고, 릴리즈 조회와 설치 실행을 분리하는 방향을 권장합니다.

## 성능 설계 메모

- Markdown 파일 렌더링 결과는 파일 정보와 렌더링 옵션 기반으로 캐싱합니다.
- 외부 파일 변경 감지는 debounce 후 처리합니다.
- 편집 중 라이브 프리뷰도 debounce 후 렌더링합니다.
- 애니메이션은 짧은 tween 또는 storyboard 중심으로 유지합니다.
- 지속적인 폴링과 무거운 `CompositionTarget.Rendering` 사용은 피합니다.
- 큰 문서에서는 `FlowDocument` 전체 재구성이 부담이 될 수 있으므로, 향후에는 문서 섹션 단위 가상화나 incremental rendering을 검토할 수 있습니다.

## 현재 한계와 확장 후보

- Mermaid 다이어그램은 구조 검토 대상이며 아직 본격 렌더링 기능은 포함되어 있지 않습니다.
- 완전한 WYSIWYG 편집기는 아니며, 현재 직관 편집은 원문과 라이브 프리뷰를 함께 보는 분할 방식입니다.
- 자동 업데이트 기능은 아직 포함되어 있지 않습니다.
- 대용량 문서의 초고속 편집을 위해서는 렌더링 범위 축소가 추가로 필요할 수 있습니다.

## 라이선스

MarkFlow는 [PolyForm Noncommercial 1.0.0](LICENSE)으로 공개됩니다. 소스 열람, 수정, 연구, 개인 및 비영리 목적의 사용과 재배포는 허용되지만 판매, 유료 재배포를 포함한 상업적 사용은 허용되지 않습니다.

이 라이선스는 OSI가 정의하는 오픈 소스 라이선스가 아니라 **source-available 라이선스**입니다. 상업적 사용 허가는 저작권자와 별도로 협의해야 합니다.
