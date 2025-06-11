# unity-multiplayer-dance-system

Unity multiplayer dance system with Photon PUN synchronization and proximity-based group dancing

## 🛠 Tech Stack

- Unity 2021.3+
- C#
- Photon PUN
- UniTask
- Coroutines
- Singleton Pattern

## ⭐ Key Features

- 근거리 플레이어 자동 감지
- 실시간 댄스 초대 시스템
- 동기화된 카운트다운 및 댄스 실행
- 오디오/애니메이션 동시 재생
- 다국어 지원 알림
- 볼륨 및 음소거 설정 연동

## 🎮 How It Works

1. 댄스 버튼 클릭 → 근거리 친구들에게 자동 초대
2. 초대 받은 친구들은 팝업으로 수락/거절 선택  
3. 카운트다운(3, 2, 1) 후 모든 참여자가 동시에 댄스
4. 댄스 중 BGM 자동 재생 및 볼륨 조절

## 🎯 System Flow

1. **근거리 감지**: Dance Radius 내 플레이어 자동 탐지
2. **초대 전송**: 선택된 플레이어들에게 RPC로 댄스 초대
3. **응답 처리**: 팝업을 통한 수락/거절 시스템
4. **동기화 실행**: 카운트다운 후 모든 참여자 동시 댄스
5. **자동 종료**: 애니메이션 완료 시 자동으로 상태 복원
