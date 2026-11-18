#!/usr/bin/env python3
"""Create the M5 Wwise event structure without importing or creating audio.

Run with the Wwise project open in Wwise Authoring (WAAPI enabled, HTTP 8090).
The script is idempotent: it merges into existing objects and never adds
duplicate event actions. Pass --no-generate to skip SoundBank generation and
--no-save to leave the Wwise project unsaved.
"""

import json
import os
import sys
import urllib.error
import urllib.request


WAAPI_URL = os.environ.get("WAAPI_HTTP_URL", "http://127.0.0.1:8090/waapi")
PLATFORMS = ["Mac", "Windows"]

WORK_UNIT = "Mejnun"
EVENTS = "\\Events\\" + WORK_UNIT
CONTAINERS = "\\Containers\\" + WORK_UNIT
SOUNDBANKS = "\\SoundBanks\\" + WORK_UNIT
GAME_PARAMETERS = "\\Game Parameters\\" + WORK_UNIT
STATES = "\\States\\" + WORK_UNIT
SWITCHES = "\\Switches\\" + WORK_UNIT

# event name -> (container folder / bus name, container name)
SFX_EVENTS = {
    "Play_UI_Click": ("UI", "UI_Click"),
    "Play_Card_Pickup": ("SFX", "Card_Pickup"),
    "Play_Card_LongPress": ("SFX", "Card_LongPress"),
    "Play_Card_Drop": ("SFX", "Card_Drop"),
    "Play_Card_Invalid": ("SFX", "Card_Invalid"),
    "Play_Card_Shuffle": ("SFX", "Card_Shuffle"),
    "Play_Card_Deal": ("SFX", "Card_Deal"),
    "Play_Card_Draw": ("SFX", "Card_Draw"),
    "Play_Meld_Valid": ("SFX", "Meld_Valid"),
    "Play_Meld_Invalid": ("SFX", "Meld_Invalid"),
    "Play_GoDown": ("SFX", "GoDown"),
    "Play_Turn_Start_Player": ("SFX", "Turn_Start_Player"),
    "Play_Turn_Start_Bot": ("SFX", "Turn_Start_Bot"),
    "Play_Turn_End": ("SFX", "Turn_End"),
    "Play_Dealer_Chosen": ("SFX", "Dealer_Chosen"),
    "Play_Undo": ("SFX", "Undo"),
    "Play_Board_Revert": ("SFX", "Board_Revert"),
    "Play_Pass": ("SFX", "Pass"),
    "Play_Timer_Tick": ("SFX", "Timer_Tick"),
    "Play_Timer_Warning": ("SFX", "Timer_Warning"),
    "Play_Timer_Halas": ("SFX", "Timer_Halas"),
    "Play_Win_Stinger": ("Music", "Win_Stinger"),
    "Play_Lose_Stinger": ("Music", "Lose_Stinger"),
    "Play_Stalemate_Stinger": ("Music", "Stalemate_Stinger"),
}

BUSES = ("Music", "SFX", "UI", "Voice")
VOLUME_PARAMETERS = {
    "Volume_Master": None,
    "Volume_Music": "Music",
    "Volume_SFX": "SFX",
    "Volume_Voice": "Voice",
}

ACTION_PLAY = 1
ACTION_STOP = 2


def call(uri, args=None, options=None):
    payload = json.dumps({"uri": uri, "args": args or {}, "options": options or {}}).encode("utf-8")
    request = urllib.request.Request(
        WAAPI_URL,
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            body = response.read().decode("utf-8")
    except urllib.error.HTTPError as error:
        raise RuntimeError("WAAPI {} failed: {}".format(uri, error.read().decode("utf-8"))) from error
    except urllib.error.URLError as error:
        raise RuntimeError(
            "Could not reach WAAPI at {}. Open the Wwise project, enable WAAPI, "
            "and check WAAPI_HTTP_URL. ({})".format(WAAPI_URL, error)
        ) from error

    try:
        result = json.loads(body)
    except ValueError as error:
        raise RuntimeError("WAAPI {} returned: {}".format(uri, body)) from error
    if isinstance(result, dict) and ("error" in result or "uri" in result and "message" in result):
        raise RuntimeError("WAAPI {} failed: {}".format(uri, body))
    return result


def get(path, fields=("name", "path", "type")):
    try:
        result = call(
            "ak.wwise.core.object.get",
            {"from": {"path": [path]}},
            {"return": list(fields)},
        )
    except RuntimeError as error:
        if "unknown_object" in str(error):
            return None
        raise
    items = result.get("return", [])
    return items[0] if items else None


def create(parent, object_type, name, children=None, properties=None):
    args = {
        "parent": parent,
        "type": object_type,
        "name": name,
        "onNameConflict": "merge",
    }
    if children:
        args["children"] = children
    if properties:
        args.update(properties)
    call("ak.wwise.core.object.create", args)
    return parent + "\\" + name


def create_work_units():
    for root in ("Events", "Containers", "SoundBanks", "Game Parameters", "States", "Switches"):
        create("\\" + root, "WorkUnit", WORK_UNIT)


def rename_object(old_path, new_path):
    old_object = get(old_path)
    new_object = get(new_path)
    if old_object and new_object:
        raise RuntimeError("Both {} and {} exist; refusing to merge them.".format(old_path, new_path))
    if old_object:
        call(
            "ak.wwise.core.object.setName",
            {"object": old_path, "value": new_path.rsplit("\\", 1)[-1]},
        )


def rename_turn_start_objects():
    rename_object(EVENTS + "\\Play_Turn_Start", EVENTS + "\\Play_Turn_Start_Player")
    rename_object(CONTAINERS + "\\SFX\\Turn_Start", CONTAINERS + "\\SFX\\Turn_Start_Player")


def create_buses():
    main_bus = "\\Busses\\Default Work Unit\\Main Audio Bus"
    if get(main_bus) is None:
        raise RuntimeError("The Wwise project has no {}.".format(main_bus))
    for name in BUSES:
        create(main_bus, "Bus", name)
    return main_bus


def bus_path(main_bus, name):
    return main_bus if name is None else main_bus + "\\" + name


def create_game_parameters(main_bus):
    for name in VOLUME_PARAMETERS:
        create(
            GAME_PARAMETERS,
            "GameParameter",
            name,
            properties={"@Min": 0, "@Max": 100, "@InitialValue": 100},
        )
    for name in ("Timer_Tension", "Board_Complexity"):
        create(
            GAME_PARAMETERS,
            "GameParameter",
            name,
            properties={"@Min": 0, "@Max": 1, "@InitialValue": 0},
        )

    # Volume_* (0-100) drives the bus volume (-96 dB .. 0 dB).
    for name, bus in VOLUME_PARAMETERS.items():
        call(
            "ak.wwise.core.object.set",
            {
                "objects": [
                    {
                        "object": bus_path(main_bus, bus),
                        "@RTPC": [
                            {
                                "type": "RTPC",
                                "name": "",
                                "@Curve": {
                                    "type": "Curve",
                                    "points": [
                                        {"x": 0, "y": -96, "shape": "Log1"},
                                        {"x": 100, "y": 0, "shape": "Linear"},
                                    ],
                                },
                                "@PropertyName": "BusVolume",
                                "@ControlInput": GAME_PARAMETERS + "\\" + name,
                            }
                        ],
                    }
                ],
                "listMode": "replaceAll",
            },
        )


def create_states_and_switches():
    create(
        STATES,
        "StateGroup",
        "Game_Phase",
        children=[{"type": "State", "name": name} for name in ("Menu", "Dealing", "Playing", "Results")],
    )
    create(
        SWITCHES,
        "SwitchGroup",
        "Shout_Type",
        children=[{"type": "Switch", "name": "Default"}],
    )


def create_containers(main_bus):
    folders = {}
    for name in BUSES:
        folders[name] = create(CONTAINERS, "Folder", name)

    def routed(bus):
        return {"@OverrideOutput": True, "@OutputBus": bus_path(main_bus, bus)}

    targets = {}
    for event, (bus, container) in SFX_EVENTS.items():
        targets[event] = create(folders[bus], "RandomSequenceContainer", container, properties=routed(bus))

    shout_group = SWITCHES + "\\Shout_Type"
    targets["Play_Mejnun_Shout"] = create(
        folders["Voice"],
        "SwitchContainer",
        "Mejnun_Shout",
        properties=dict(
            routed("Voice"),
            **{
                "@SwitchGroupOrStateGroup": shout_group,
                "@DefaultSwitchOrState": shout_group + "\\Default",
            }
        ),
    )
    targets["Music_Menu"] = create(folders["Music"], "MusicPlaylistContainer", "Menu_Music", properties=routed("Music"))
    targets["Music_Game"] = create(folders["Music"], "MusicPlaylistContainer", "Game_Music", properties=routed("Music"))
    return targets


def create_event(name, actions):
    path = EVENTS + "\\" + name
    if get(path) is not None:
        return path
    create(
        EVENTS,
        "Event",
        name,
        children=[
            {"type": "Action", "name": str(index), "@ActionType": action_type, "@Target": target}
            for index, (action_type, target) in enumerate(actions)
        ],
    )
    return path


def create_events(targets):
    events = []
    for name in list(SFX_EVENTS) + ["Play_Mejnun_Shout", "Music_Menu", "Music_Game"]:
        events.append(create_event(name, [(ACTION_PLAY, targets[name])]))
    events.append(
        create_event(
            "Stop_Music",
            [(ACTION_STOP, targets["Music_Menu"]), (ACTION_STOP, targets["Music_Game"])],
        )
    )
    return events


def create_soundbank(events):
    soundbank = create(SOUNDBANKS, "SoundBank", "Main")
    call(
        "ak.wwise.core.soundbank.setInclusions",
        {
            "soundbank": soundbank,
            "operation": "replace",
            "inclusions": [{"object": EVENTS, "filter": ["events", "structures", "media"]}],
        },
    )
    return soundbank


def generate_soundbanks():
    result = call(
        "ak.wwise.core.soundbank.generate",
        {
            "soundbanks": [{"name": "Main"}],
            "platforms": PLATFORMS,
            "rebuildInitBank": True,
            "writeToDisk": True,
        },
    )
    return result


def main():
    create_work_units()
    rename_turn_start_objects()
    main_bus = create_buses()
    create_game_parameters(main_bus)
    create_states_and_switches()
    targets = create_containers(main_bus)
    events = create_events(targets)
    create_soundbank(events)
    if "--no-save" not in sys.argv:
        call("ak.wwise.core.project.save")
    print("Created {} events in {} and the Main SoundBank.".format(len(events), EVENTS))

    if "--no-generate" not in sys.argv:
        result = generate_soundbanks()
        logs = result.get("logs", []) if isinstance(result, dict) else []
        for entry in logs:
            print("{}: {}".format(entry.get("severity", ""), entry.get("message", "")))
        print("Generated Init and Main for {}.".format(", ".join(PLATFORMS)))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(error, file=sys.stderr)
        sys.exit(1)
