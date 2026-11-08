#ifndef AuraFlashXRCLIENT_PACKETTYPES_H
#define AuraFlashXRCLIENT_PACKETTYPES_H
#include "../auraflashxr_server/bindings.h"
#include <assert.h>
#include <stdint.h>

enum AuraFlashXR_CODEC {
    AuraFlashXR_CODEC_H264 = 0,
    AuraFlashXR_CODEC_HEVC = 1,
    AuraFlashXR_CODEC_AV1 = 2,
};

enum AuraFlashXR_H264_PROFILE {
    AuraFlashXR_H264_PROFILE_HIGH = 0,
    AuraFlashXR_H264_PROFILE_MAIN = 1,
    AuraFlashXR_H264_PROFILE_BASELINE = 2,
};

enum AuraFlashXR_RATE_CONTROL_METHOD {
    AuraFlashXR_CBR = 0,
    AuraFlashXR_VBR = 1,
};

enum AuraFlashXR_ENTROPY_CODING {
    AuraFlashXR_CABAC = 0,
    AuraFlashXR_CAVLC = 1,
};

enum AuraFlashXR_ENCODER_QUALITY_PRESET { AuraFlashXR_QUALITY = 0, AuraFlashXR_BALANCED = 1, AuraFlashXR_SPEED = 2 };

enum AuraFlashXR_INPUT {
    AuraFlashXR_INPUT_FINGER_INDEX,
    AuraFlashXR_INPUT_FINGER_MIDDLE,
    AuraFlashXR_INPUT_FINGER_RING,
    AuraFlashXR_INPUT_FINGER_PINKY,
};
#define AuraFlashXR_BUTTON_FLAG(input) (1ULL << input)

#endif // AuraFlashXRCLIENT_PACKETTYPES_H
