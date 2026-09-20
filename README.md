# 배달 라이더 게임 (가제)

3D 배달 라이더 시뮬레이션 게임 프로젝트입니다. 플레이어는 폰 UI로 배달 콜을 받아 가게에서 음식을 픽업하고, 제한 시간 안에 배달지까지 이동해 배달비를 법니다. 번 돈으로 더 빠른 탈것과 악세서리를 구매하며, 체력/연료 자원을 관리하는 것이 핵심 루프입니다.

자세한 기획 내용과 시스템 설계는 프로젝트에 연결된 Claude Docs 기획 문서를 참고하세요 (Unity Projects 프로젝트 안에서 확인 가능합니다).

## 시작하기

1. Unity Hub에서 Unity 6 LTS(URP)로 새 3D 프로젝트를 생성합니다.
2. 이 저장소의 `.gitignore`와 `.gitattributes`를 프로젝트 루트에 복사합니다.
3. `git lfs install`을 한 번 실행한 뒤, `create_folders.sh`를 프로젝트 루트에서 실행해 폴더 구조를 만듭니다.
   ```
   bash create_folders.sh
   ```
4. `git init`, `git add .`, 첫 커밋, GitHub 원격 저장소 연결 순서로 진행합니다.

## 폴더 구조

```
Assets/_Project/
  Scripts/    (Player, Vehicles, Delivery, UI, Systems)
  Prefabs/    (Vehicles, Characters, Props, UI)
  Scenes/
  Art/        (Vehicles, Characters, Environment, Accessories)
  Materials/
  Audio/      (SFX, Music)
  Settings/
```

## 커밋 컨벤션

- `feat:` 새 기능
- `fix:` 버그 수정
- `chore:` 설정/폴더 정리 등 잡일

## 마일스톤

| 단계 | 내용 |
| --- | --- |
| M1 | 프로젝트 초기 세팅, 기본 이동 |
| M2 | 콜 시스템 프로토타입 |
| M3 | 픽업 상호작용 |
| M4 | 배달/정산 로직 |
| M5 | 체력/부스터 시스템 |
| M6 | 탈것/연료 시스템 |
| M7 | 커스터마이징 상점 |
| M8 | 비주얼 폴리싱 (VARCO 3D 에셋 적용) |
