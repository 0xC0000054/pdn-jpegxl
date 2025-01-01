////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024, 2025 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

#include "Common.h"
#include <stdio.h>
#include <stdarg.h>
#include <string.h>

void SetErrorMessage(ErrorInfo* errorInfo, const char* message)
{
    if (errorInfo && message)
    {
        const size_t errorMessageLength = strlen(message);

        if (errorMessageLength > 0 && errorMessageLength <= ErrorInfo::maxErrorMessageLength)
        {
            strncpy_s(errorInfo->errorMessage, message, errorMessageLength);
        }
    }
}

void SetErrorMessageFormat(ErrorInfo* errorInfo, const char* format, ...)
{
    if (errorInfo && format)
    {
        va_list args1;

        va_start(args1, format);

        va_list args2;
        va_copy(args2, args1);

        const int formattedStringLength = _vscprintf(format, args1);

        va_end(args1);

        if (formattedStringLength > 0 && formattedStringLength <= ErrorInfo::maxErrorMessageLength)
        {
            vsprintf_s(errorInfo->errorMessage, format, args2);
        }

        va_end(args2);
    }
}
