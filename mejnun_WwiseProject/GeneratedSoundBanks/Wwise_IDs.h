/////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Audiokinetic Wwise generated include file. Do not edit.
//
/////////////////////////////////////////////////////////////////////////////////////////////////////

#ifndef __WWISE_IDS_H__
#define __WWISE_IDS_H__

#include <AK/SoundEngine/Common/AkTypes.h>

namespace AK
{
    namespace EVENTS
    {
        static const AkUniqueID MUSIC_GAME = 1072299391U;
        static const AkUniqueID MUSIC_MENU = 1598298728U;
        static const AkUniqueID PLAY_BOARD_REVERT = 1150067407U;
        static const AkUniqueID PLAY_CARD_DEAL = 2530612017U;
        static const AkUniqueID PLAY_CARD_DRAW = 2796935317U;
        static const AkUniqueID PLAY_CARD_DROP = 2830490744U;
        static const AkUniqueID PLAY_CARD_INVALID = 3991713996U;
        static const AkUniqueID PLAY_CARD_LONGPRESS = 4091023330U;
        static const AkUniqueID PLAY_CARD_PICKUP = 2749464947U;
        static const AkUniqueID PLAY_CARD_SHUFFLE = 4220340206U;
        static const AkUniqueID PLAY_DEALER_CHOSEN = 1858408314U;
        static const AkUniqueID PLAY_GODOWN = 2113182384U;
        static const AkUniqueID PLAY_LOSE_STINGER = 3606041870U;
        static const AkUniqueID PLAY_MEJNUN_SHOUT = 2825832111U;
        static const AkUniqueID PLAY_MELD_INVALID = 2198168116U;
        static const AkUniqueID PLAY_MELD_VALID = 3179716423U;
        static const AkUniqueID PLAY_PASS = 1885680669U;
        static const AkUniqueID PLAY_STALEMATE_STINGER = 3818448697U;
        static const AkUniqueID PLAY_TIMER_HALAS = 1457389367U;
        static const AkUniqueID PLAY_TIMER_TICK = 4093125639U;
        static const AkUniqueID PLAY_TIMER_WARNING = 98812124U;
        static const AkUniqueID PLAY_TURN_END = 927528689U;
        static const AkUniqueID PLAY_TURN_START_BOT = 3588254728U;
        static const AkUniqueID PLAY_TURN_START_PLAYER = 3780346990U;
        static const AkUniqueID PLAY_UI_CLICK = 1749424733U;
        static const AkUniqueID PLAY_UNDO = 770096812U;
        static const AkUniqueID PLAY_WIN_STINGER = 4014770395U;
        static const AkUniqueID STOP_MUSIC = 2837384057U;
        static const AkUniqueID TEST = 3157003241U;
    } // namespace EVENTS

    namespace STATES
    {
        namespace GAME_PHASE
        {
            static const AkUniqueID GROUP = 3211383847U;

            namespace STATE
            {
                static const AkUniqueID DEALING = 959498469U;
                static const AkUniqueID MENU = 2607556080U;
                static const AkUniqueID NONE = 748895195U;
                static const AkUniqueID PLAYING = 1852808225U;
                static const AkUniqueID RESULTS = 3780578133U;
            } // namespace STATE
        } // namespace GAME_PHASE

    } // namespace STATES

    namespace SWITCHES
    {
        namespace SHOUT_TYPE
        {
            static const AkUniqueID GROUP = 3140450991U;

            namespace SWITCH
            {
                static const AkUniqueID DEFAULT = 782826392U;
            } // namespace SWITCH
        } // namespace SHOUT_TYPE

    } // namespace SWITCHES

    namespace GAME_PARAMETERS
    {
        static const AkUniqueID BOARD_COMPLEXITY = 3269148114U;
        static const AkUniqueID TIMER_TENSION = 3596532595U;
        static const AkUniqueID VOLUME_MASTER = 3695994288U;
        static const AkUniqueID VOLUME_MUSIC = 3891337659U;
        static const AkUniqueID VOLUME_SFX = 3673881719U;
        static const AkUniqueID VOLUME_VOICE = 3676256460U;
    } // namespace GAME_PARAMETERS

    namespace BANKS
    {
        static const AkUniqueID INIT = 1355168291U;
        static const AkUniqueID DEFAULT_SOUNDBANK = 2218482606U;
        static const AkUniqueID MAIN = 3161908922U;
    } // namespace BANKS

    namespace BUSSES
    {
        static const AkUniqueID MAIN_AUDIO_BUS = 2246998526U;
        static const AkUniqueID MUSIC = 3991942870U;
        static const AkUniqueID SFX = 393239870U;
        static const AkUniqueID UI = 1551306167U;
        static const AkUniqueID VOICE = 3170124113U;
    } // namespace BUSSES

    namespace AUDIO_DEVICES
    {
        static const AkUniqueID NO_OUTPUT = 2317455096U;
        static const AkUniqueID SYSTEM = 3859886410U;
    } // namespace AUDIO_DEVICES

}// namespace AK

#endif // __WWISE_IDS_H__
