#pragma once

#include <string>

#include <gzip/decompress.hpp>
#include <gzip/utils.hpp>

inline std::string DecompressFromFile(winrt::hstring filePath)
{
    FILE* file = _wfopen(filePath.c_str(), L"rb");
    if (file == NULL)
    {
        return "";
    }

    fseek(file, 0, SEEK_END);
    size_t length = ftell(file);
    fseek(file, 0, SEEK_SET);
    char* buffer = (char*)malloc(length);
    fread(buffer, 1, length, file);
    fclose(file);

    std::string data;

    bool compressed = gzip::is_compressed(buffer, length);
    if (compressed)
    {
        data = gzip::decompress(buffer, length);
    }
    else
    {
        data = std::string(buffer, length);
    }

    free(buffer);
    return data;
}
