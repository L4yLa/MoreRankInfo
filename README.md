# MoreRankInfo

Beat Saber のリーダーボードの上に、PP とランキングの変化を表示する BSIPA プラグインです。

```
[アイコン] Today +12.34pp (+0.112%)
           #1234 (+5)   JP #56 (+1)
           LastGain: +3.21pp (+0.03%)
           曲名 - マッパー名 [Expert+]
```

## 表示内容

| 行 | 内容 |
|---|---|
| Today | 当日の総PPの増加量と、当日開始時の総PPに対する増加率 |
| 順位 | グローバル順位と国内順位、それぞれの当日の変動（設定で非表示にできます） |
| LastGain | 直近で総PPが変わったスコア更新による総PPの増加量と、そのプレイ前の総PPに対する増加率 |
| 曲情報 | LastGain のプレイの曲名・マッパー名・難易度。リーダーボードの幅に収まらない場合は横にスクロールします |

- 「当日」は設定した時刻（既定は 3 時）で切り替わり、その日最初に取得した値を基準にします。
- LastGain は、総PPを伸ばしたスコア更新だけを記録します（自己ベスト未更新や非ランク譜面では更新されません）。
- 表示中のリーダーボード ScoreSaber / BeatLeader に合わせて情報を表示します。

## 動作環境

- Beat Saber 1.44.1（Steam）
- BSIPA 4.3.7 以上
- BeatSaberMarkupLanguage 1.14.1 以上
- SiraUtil 3.4.0 以上
- ScoreSaber と BeatLeader のどちらか、または両方

開発時の環境: ScoreSaber 3.4.2 / BeatLeader 0.10.0 / LeaderboardCore 1.7.0

## インストール

1. [Releases](https://github.com/L4yLa/MoreRankInfo/releases) から `MoreRankInfo_v<バージョン>_bs1.44.1.7z` をダウンロードします。
2. Beat Saber のフォルダに展開し、`Plugins\MoreRankInfo.dll` が置かれることを確認します。

## 設定

メインメニュー左側の MOD ボタン一覧にある **MoreRankInfo** から開きます。

| 項目 | 既定値 | 説明 |
|---|---|---|
| Date change hour | 3 | 「当日」が切り替わる時刻（0〜23 時） |
| Show ranks | ON | 順位の行を表示する |
| Max font size | 10 | LastGain の文字サイズの上限。他の行はこれより小さく、リーダーボードの幅に収まるよう自動で縮みます |
| Vertical offset | 0 | 表示位置の上下の調整 |


## ビルド

ビルドには [BeatSaberModdingTools.Tasks](https://github.com/Zingabopp/BeatSaberModdingTools.Tasks) を使用します。
プロジェクトと同じフォルダに `MoreRankInfo.csproj.user` を作成し、Beat Saber 1.44.1 のフォルダを指定してください。

```xml
<Project ToolsVersion="Current">
  <PropertyGroup Condition="'$(BSTarget)' == '1.44.1'">
    <BeatSaberDir>C:\Path\To\Beat Saber</BeatSaberDir>
  </PropertyGroup>
</Project>
```

```
dotnet build MoreRankInfo.csproj -c Release -p:Platform=BS1.44.1
```

出力先は `bin\1.44.1\Release\MoreRankInfo.dll` です。


## ライセンス

[MIT License](LICENSE)

---

## English

A BSIPA plugin that shows your pp and rank changes above the Beat Saber leaderboard.

### What is shown

| Line | Content |
|---|---|
| Today | Today's total pp gain and its rate relative to the total pp at the start of the day |
| Ranks | Global and country rank with today's change (can be hidden in the settings) |
| LastGain | Total pp gained by the most recent score update that changed your total pp, and its rate relative to the total pp before that play |
| Song | Song name, mapper and difficulty of that play. Scrolls horizontally when it does not fit the leaderboard width |

- "Today" starts at the configured hour (3:00 by default); the first value fetched on that day is the baseline.
- LastGain only records score updates that increased your total pp (it is not updated when you do not set a personal best or play an unranked map).
- The information follows the leaderboard currently shown (ScoreSaber / BeatLeader).

### Requirements

- Beat Saber 1.44.1 (Steam)
- BSIPA 4.3.7+
- BeatSaberMarkupLanguage 1.14.1+
- SiraUtil 3.4.0+
- ScoreSaber and/or BeatLeader

Developed with: ScoreSaber 3.4.2 / BeatLeader 0.10.0 / LeaderboardCore 1.7.0

### Installation

1. Download `MoreRankInfo_v<version>_bs1.44.1.7z` from [Releases](https://github.com/L4yLa/MoreRankInfo/releases).
2. Extract it into your Beat Saber folder and make sure `Plugins\MoreRankInfo.dll` exists.

### Settings

Open **MoreRankInfo** from the mod button list on the left of the main menu.

| Setting | Default | Description |
|---|---|---|
| Date change hour | 3 | Hour (0-23) at which "today" starts |
| Show ranks | ON | Show the rank line |
| Max font size | 10 | Upper limit of the LastGain text size. Other lines are smaller, and text shrinks automatically to fit the leaderboard width |
| Vertical offset | 0 | Moves the display up or down |

### Building

Building uses [BeatSaberModdingTools.Tasks](https://github.com/Zingabopp/BeatSaberModdingTools.Tasks).
Create `MoreRankInfo.csproj.user` next to the project and point it to your Beat Saber 1.44.1 folder.

```xml
<Project ToolsVersion="Current">
  <PropertyGroup Condition="'$(BSTarget)' == '1.44.1'">
    <BeatSaberDir>C:\Path\To\Beat Saber</BeatSaberDir>
  </PropertyGroup>
</Project>
```

```
dotnet build MoreRankInfo.csproj -c Release -p:Platform=BS1.44.1
```

The output is `bin\1.44.1\Release\MoreRankInfo.dll`.

### License

[MIT License](LICENSE)
