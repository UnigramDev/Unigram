<?xml version="1.0" encoding="utf-8"?>
<!--
  The parts of Package.appxmanifest that the Win32 flavour cannot keep, removed rather than
  patched: XmlPoke can only set values, so the csproj does the renames and this does the deletions.
  One manifest stays the source of truth either way - a second copy would drift, which is the
  reason Telegram.Modern.csproj patches rather than duplicates.
-->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:f="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
                xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">

  <xsl:output method="xml" indent="yes" />

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <!--
    A full trust application cannot host an in-process app service - registration fails with
    0x80080204, the extension wanting an EntryPoint it has no way to name. Telegram.Stub's bridge
    is on the list to retire here anyway (item 2.5), so it goes rather than being ported.
  -->
  <xsl:template match="uap:Extension[@Category='windows.appService']" />

  <!--
    The UWP app model capabilities, which mean nothing outside an AppContainer: a full trust app
    reaches the pictures library and removable storage through the user's own token, closes without
    confirmAppClose, and has no one-process VoIP model. Item 2.4.
  -->
  <xsl:template match="rescap:Capability[@Name='oneProcessVoIP']" />
  <xsl:template match="rescap:Capability[@Name='confirmAppClose']" />
  <xsl:template match="rescap:Capability[@Name='packageManagement']" />
  <xsl:template match="uap:Capability[@Name='removableStorage']" />
  <xsl:template match="uap:Capability[@Name='picturesLibrary']" />

  <!--
    And the one it does run on, which has to be added rather than renamed into place. The schema
    orders this element: every Capability comes before the first DeviceCapability, so it is
    inserted between the two groups rather than appended.
  -->
  <xsl:template match="f:Capabilities">
    <xsl:copy>
      <xsl:apply-templates select="@*" />
      <xsl:apply-templates select="node()[not(self::f:DeviceCapability)]" />
      <rescap:Capability Name="runFullTrust" />
      <xsl:apply-templates select="f:DeviceCapability" />
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
