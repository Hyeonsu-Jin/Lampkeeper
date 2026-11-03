# Unity Smart Merge(UnityYAMLMerge) 등록 안내

- 대상: 개발 PC마다 한 번 (저장소 로컬 git 설정)
- 관련: SDD M1-01 (4), DES-112, NFR-043
- 작성: 2026-11-03 (M1-01), 같은 날 실제 파일 시험 결과로 **mergetool 방식**으로 변경

## 1. 저장소에 이미 들어 있는 것

저장소 루트 `.gitattributes`에 Unity YAML 에셋(`*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.mat`, `*.anim`, `*.controller` 등)이 `merge=unityyamlmerge`로 지정되어 있습니다. 이 파일은 고치지 않습니다.

`merge=unityyamlmerge`는 이름이 같은 **merge 드라이버**가 등록되어 있을 때만 쓰입니다. 아래 이유로 드라이버는 등록하지 않으므로, 병합은 평소처럼 git의 일반 글자 병합으로 시작하고, 충돌이 나면 `git mergetool`로 UnityYAMLMerge를 부릅니다.

## 2. 왜 드라이버(SDD 원래 값)를 쓰지 않는가: 시험 결과 (2026-11-03)

SDD M1-01에 적힌 `merge.unityyamlmerge.driver = "'…/UnityYAMLMerge.exe' merge -p %O %B %A %A"`를 임시 저장소에서 Unity가 만든 실제 Force Text 에셋(`Boot.unity`)으로 시험했습니다. 한 브랜치는 오브젝트 A의 이름을, 다른 브랜치는 오브젝트 B의 이름을 고쳤습니다.

- 결과: **자동 병합 실패**. `Couldn't locate merge tool to handle extension merge_file_xxxx in …\mergespecfile.txt` 오류 후 git이 충돌로 표시(`UU`). 이전 시험(내용이 비YAML인 경우)과 같은 결과이므로 내용 문제가 아니라 **확장자 문제**입니다.
- 원인: git은 드라이버에 `.merge_file_xxxx` 같은 **확장자 없는 임시 파일**을 넘기고, UnityYAMLMerge는 확장자로 병합 규칙을 찾습니다.
- 확장자가 있는 파일을 직접 넘기면 같은 병합이 충돌 없이 성공합니다(이전 시험).
- 오류 시 도구가 모달 오류 창을 띄웁니다. 드라이버 방식은 병합할 때마다 창이 뜰 수 있어 쓰지 않습니다.

## 3. 각 PC에서 할 일: mergetool 등록 (저장소 로컬)

저장소 루트에서 실행합니다. `--global`은 쓰지 않습니다. Unity 설치 경로가 다르면 `cmd` 줄의 경로만 바꿉니다(Unity 버전을 올릴 때는 버전 폴더도).

```bash
git config --local merge.tool unityyamlmerge
git config --local mergetool.unityyamlmerge.trustExitCode false
git config --local mergetool.unityyamlmerge.keepBackup false
git config --local mergetool.unityyamlmerge.cmd "'C:/Program Files/Unity/Hub/Editor/6000.3.27f2/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p \"\$BASE\" \"\$REMOTE\" \"\$LOCAL\" \"\$MERGED\""
```

(예전에 SDD 값으로 드라이버를 등록했다면 지웁니다: `git config --local --unset merge.unityyamlmerge.driver`, `git config --local --unset merge.unityyamlmerge.name`.)

## 4. 사용

```bash
git merge <브랜치>        # 충돌 나면 파일이 UU로 표시됨
git mergetool             # 충돌 파일마다 UnityYAMLMerge 실행 (Enter로 진행)
git commit
```

git mergetool은 `파일명_BASE_번호.unity`처럼 **확장자가 있는 임시 파일**을 도구에 넘깁니다. **이 방식은 아직 시험하지 않았습니다.** 첫 실제 충돌 때 결과(자동 해결 여부, 오류 창 여부)를 이 문서에 적어 주세요. 도구가 해결하지 못한 충돌은 오류 창이 뜨거나 충돌로 남습니다. 이때는 Unity 에디터에서 직접 고칩니다.

## 5. 확인

```bash
git config --local --get-regexp "mergetool.unityyamlmerge|merge.tool"
git check-attr merge -- src/Assets/_Game/Scenes/Boot.unity     # 결과: merge: unityyamlmerge
```

## 6. 시험할 때 주의

- 병합 시험은 Unity가 Force Text로 저장한 실제 YAML 에셋으로, 프로젝트 저장소가 아닌 임시 저장소에서만 합니다.
- 도구는 오류가 나면 모달 창을 띄웁니다(자동 시험에 쓸 때는 `-h` 옵션으로 창을 막을 수 있음).
