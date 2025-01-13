// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#include "integration.h"

#include <regex>
#include <sstream>

#include <unordered_map>

#include "logger.h"
#include "util.h"

namespace trace
{

std::mutex m_assemblyReferenceCacheMutex;
std::unordered_map<WSTRING, std::unique_ptr<AssemblyReference>> m_assemblyReferenceCache;

AssemblyReference::AssemblyReference(const WSTRING& str)
    : name(GetNameFromAssemblyReferenceString(str))
    , version(GetVersionFromAssemblyReferenceString(str))
    , locale(GetLocaleFromAssemblyReferenceString(str))
    , public_key(GetPublicKeyFromAssemblyReferenceString(str))
{
}

AssemblyReference* AssemblyReference::GetFromCache(const WSTRING& str)
{
    std::lock_guard<std::mutex> guard(m_assemblyReferenceCacheMutex);
    auto                        findRes = m_assemblyReferenceCache.find(str);
    if (findRes != m_assemblyReferenceCache.end())
    {
        return findRes->second.get();
    }
    AssemblyReference* aref       = new AssemblyReference(str);
    m_assemblyReferenceCache[str] = std::unique_ptr<AssemblyReference>(aref);
    return aref;
}

namespace
{

WSTRING GetNameFromAssemblyReferenceString(const WSTRING& wstr)
{
    WSTRING name = wstr;

    auto pos = name.find(WStr(','));
    if (pos != WSTRING::npos)
    {
        name = name.substr(0, pos);
    }

    // strip spaces
    pos = name.rfind(WStr(' '));
    if (pos != WSTRING::npos)
    {
        name = name.substr(0, pos);
    }

    return name;
}

Version GetVersionFromAssemblyReferenceString(const WSTRING& str)
{
    unsigned short major    = 0;
    unsigned short minor    = 0;
    unsigned short build    = 0;
    unsigned short revision = 0;

    if (str.empty())
    {
        return {major, minor, build, revision};
    }

    static auto re = std::wregex(WStr("Version=([0-9]+)\\.([0-9]+)\\.([0-9]+)\\.([0-9]+)"));

    std::wsmatch match;
    if (std::regex_search(str, match, re) && match.size() == 5)
    {
        WSTRINGSTREAM(match.str(1)) >> major;
        WSTRINGSTREAM(match.str(2)) >> minor;
        WSTRINGSTREAM(match.str(3)) >> build;
        WSTRINGSTREAM(match.str(4)) >> revision;
    }

    return {major, minor, build, revision};
}

WSTRING GetLocaleFromAssemblyReferenceString(const WSTRING& str)
{
    WSTRING locale = WStr("neutral");

    if (str.empty())
    {
        return locale;
    }

    static auto  re = std::wregex(WStr("Culture=([a-zA-Z0-9]+)"));
    std::wsmatch match;
    if (std::regex_search(str, match, re) && match.size() == 2)
    {
        locale = match.str(1);
    }

    return locale;
}

PublicKey GetPublicKeyFromAssemblyReferenceString(const WSTRING& str)
{
    BYTE data[8] = {0};

    if (str.empty())
    {
        return PublicKey(data);
    }

    static auto  re = std::wregex(WStr("PublicKeyToken=([a-fA-F0-9]{16})"));
    std::wsmatch match;
    if (std::regex_search(str, match, re) && match.size() == 2)
    {
        for (int i = 0; i < 8; i++)
        {
            auto          s = match.str(1).substr(i * 2, 2);
            unsigned long x;
            WSTRINGSTREAM(s) >> std::hex >> x;
            data[i] = BYTE(x);
        }
    }

    return PublicKey(data);
}

} // namespace

} // namespace trace
