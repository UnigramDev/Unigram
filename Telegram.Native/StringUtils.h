#pragma once

#include <string>

#ifndef STRING_UTILS_H
#define STRING_UTILS_H

template<typename... ARGS>
class wstrprintf : public std::basic_string<wchar_t>
{
public:
    wstrprintf(const wchar_t* format, ARGS... args)
    {
        int len = _scwprintf(format, args...);
        resize(len);
        swprintf_s((wchar_t* const)c_str(), len + 1, format, args...);
    }
};

#endif
