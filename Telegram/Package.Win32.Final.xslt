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

  <!-- Microsoft.VCLibs.140.00 in Release, .Debug in Debug. -->
  <xsl:param name="VCLibsName" select="'Microsoft.VCLibs.140.00'" />

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <!--
    The store CRT the C++/WinRT components link against. Without it RLottie.dll and
    Telegram.Native.dll fail to load inside the package, and CsWinRT reports that as
    REGDB_E_CLASSNOTREG out of ActivationFactory.ManifestFreeGet - which reads like a registration
    problem and is not one. Telegram.Modern.csproj has this entry generated for it; this project
    does not, and that difference is the whole reason the app could not start.
  -->
  <xsl:template match="f:Dependencies">
    <xsl:copy>
      <xsl:apply-templates select="@*" />
      <xsl:apply-templates select="f:TargetDeviceFamily" />
      <PackageDependency Name="{$VCLibsName}" MinVersion="14.0.33519.0"
                         Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />
      <xsl:apply-templates select="node()[not(self::f:TargetDeviceFamily)]" />
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
