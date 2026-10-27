<?xml version="1.0" encoding="utf-8"?>
<!--
  Applied to the *generated* AppxManifest.xml rather than to Package.appxmanifest, because the
  packaging targets append their own TargetDeviceFamily and PackageDependency entries after the
  source manifest has been read. Dependencies is an ordered element - every TargetDeviceFamily
  before the first PackageDependency - so anything inserted earlier ends up in the wrong place and
  fails schema validation with 0xC00CE014.
-->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:f="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">

  <xsl:output method="xml" indent="yes" />

  <!-- Microsoft.VCLibs.140.00 in Release, .Debug in Debug; empty for a payload with no
       uwp-triplet libraries, the WinUI flavour's. -->
  <xsl:param name="VCLibsName" select="'Microsoft.VCLibs.140.00'" />

  <!-- Microsoft.VCLibs.140.00.UWPDesktop in Release; empty in Debug, where Visual Studio's own
       debug CRT is what the components load. -->
  <xsl:param name="DesktopVCLibsName" select="''" />

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <!--
    Two CRTs, because the payload links against both. The vcpkg libraries are built for the UWP
    triplet and import the store CRT (VCRUNTIME140_APP.dll); the two C++/WinRT components and the
    wallet engine are desktop binaries and import the desktop one (VCRUNTIME140.dll). Neither is in
    the package, and Microsoft.VCLibs.140.00 holds only the _app half, so a machine without the
    Visual C++ redistributable in System32 cannot load Telegram.Native.dll.

    A component that fails to load surfaces as REGDB_E_CLASSNOTREG out of
    ActivationFactory.ManifestFreeGet - which reads like a registration problem and is not one.
  -->
  <xsl:template match="f:Dependencies">
    <xsl:copy>
      <xsl:apply-templates select="@*" />
      <xsl:apply-templates select="f:TargetDeviceFamily" />
      <xsl:if test="$VCLibsName != ''">
        <PackageDependency Name="{$VCLibsName}" MinVersion="14.0.33519.0"
                           Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />
      </xsl:if>
      <xsl:if test="$DesktopVCLibsName != ''">
        <PackageDependency Name="{$DesktopVCLibsName}" MinVersion="14.0.33728.0"
                           Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />
      </xsl:if>
      <xsl:apply-templates select="node()[not(self::f:TargetDeviceFamily)]" />
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
