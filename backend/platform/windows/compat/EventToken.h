// MinGW's headers lack EventToken.h, which WebView2.h includes. This matches the definition in the Windows SDK.
#pragma once

typedef struct EventRegistrationToken {
    __int64 value;
} EventRegistrationToken;
