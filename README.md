# vr-piano-pseudohaptics
A VR piano system for Meta Quest using OpenXR hand tracking and pseudo-haptic interaction.

## 프로젝트 소개

**실제 피아노 없이, 책상 위에서 손가락으로 연주하는 VR 피아노 프로젝트입니다.**

Meta Quest VR 헤드셋이 양손의 움직임을 추적해, 컨트롤러를 쥐지 않고 가상 피아노를 연주할 수 있습니다. 현실에서는 손끝이 책상에 닿고, 가상 공간에서는 건반이 눌리며 피아노 소리가 납니다.

공중에 손을 계속 띄우는 연주 방식의 부담을 줄이고, 가상 건반을 실제로 누르는 듯한 느낌을 만드는 것이 목표입니다. 현재는 이 상호작용을 구현하고 탐색하는 프로토타입입니다.

## 핵심 아이디어: 책상의 접촉감 + 의사 햅틱

- **수동 햅틱(Passive Haptics)**: 실제 책상에 손이 닿을 때 느끼는 접촉감을 활용합니다.
- **의사 햅틱(Pseudo-Haptics)**: 화면 속 건반이 눌리는 정도와 속도를 조절해, 건반의 무게감이나 저항감을 느끼도록 유도하는 방식입니다. 이 프로젝트에서는 손가락이 내려오는 속도와 건반을 누른 위치를 건반 움직임에 반영합니다.

책상이 실제 접촉감을 제공하고, 가상 건반의 움직임과 소리가 연주 동작에 반응하도록 구성했습니다.

## 어떻게 연주하나요?

1. **위치 맞추기** — 헤드셋을 착용하고 책상 위에 양손 손끝을 비슷한 높이로 올려둡니다. 손 위치를 잠시 유지하면 카운트다운 후 손끝의 위치와 높이를 기준으로 가상 피아노가 배치됩니다.
2. **건반 누르기** — 손가락을 들어 올렸다가 원하는 건반 위치를 두드리면, 해당 건반이 움직이며 소리가 납니다. 양손의 여러 손가락으로 여러 건반을 함께 누를 수 있습니다.
3. **손 떼기** — 손가락을 떼면 건반이 원래 위치로 돌아오고 소리는 짧게 잦아듭니다.

## 연주에 따라 달라지는 반응

- **누르는 속도**: 손가락이 접촉하기 직전의 속도에 따라 건반의 초기 눌림 정도와 이후 내려가는 속도, 소리의 크기가 달라집니다.
- **누르는 위치**: 같은 속도로 눌러도 건반의 앞쪽과 뒤쪽이 다르게 반응하도록 조정했습니다. 회전축에 가까운 뒤쪽은 상대적으로 무겁고, 앞쪽은 가볍게 느껴지도록 의도했습니다.
- **88개 건반**: 각 건반에 피아노 음원을 연결하고, 건반별로 눌림과 소리 재생을 처리합니다.

## 시연 영상


https://github.com/user-attachments/assets/5c1ce951-e740-4bed-a422-debdceaced5f



## Piano Asset Notice
A Unity Asset Store piano asset was used in this project.
The asset files are not included in this repository
because they are excluded via `.gitignore` due to license restrictions.
If needed, the asset must be imported separately in the local Unity environment.

Source: [Grand Piano](https://assetstore.unity.com/packages/3d/props/grand-piano-114139)

## Audio Assets
Piano samples used in this project are from **MISStereoPiano** by **neatonk** on Freesound.  

Source: [Freesound Pack](https://freesound.org/people/neatonk/packs/9133/)  
Original source referenced by the pack: [University of Iowa Electronic Music Studios](http://theremin.music.uiowa.edu/)
