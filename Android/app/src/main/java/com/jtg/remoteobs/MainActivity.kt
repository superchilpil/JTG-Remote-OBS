package com.jtg.remoteobs

import android.app.Activity
import android.graphics.Typeface
import android.os.Bundle
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView

class MainActivity : Activity() {
    private val background = 0xFF101216.toInt()
    private val surface = 0xFF1A1E25.toInt()
    private val textPrimary = 0xFFF1F3F6.toInt()
    private val textMuted = 0xFF9AA4B2.toInt()
    private val accent = 0xFFE65A55.toInt()
    private lateinit var content: LinearLayout

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        showDashboard()
    }

    private fun baseScreen(title: String, subtitle: String) {
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(20, 28, 20, 16)
            setBackgroundColor(background)
        }
        root.addView(label("JTG REMOTE OBS", 12f, accent, true))
        root.addView(label(title, 27f, textPrimary, true))
        root.addView(label(subtitle, 13f, textMuted))
        val scroll = ScrollView(this)
        content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }
        scroll.addView(content)
        root.addView(scroll, LinearLayout.LayoutParams( ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f))

        val nav = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER
            setPadding(0, 12, 0, 0)
        }
        nav.addView(navButton("HOME") { showDashboard() }, weightParams())
        nav.addView(navButton("LIVE CHAT") { showChat() }, weightParams())
        nav.addView(navButton("SETTINGS") { showSettings() }, weightParams())
        root.addView(nav)
        setContentView(root)
    }

    private fun weightParams() = LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)

    private fun navButton(title: String, action: () -> Unit) = Button(this).apply {
        text = title
        textSize = 10f
        setTextColor(textMuted)
        setBackgroundColor(background)
        setOnClickListener { action() }
    }

    private fun label(value: String, size: Float, color: Int, bold: Boolean = false) =
        TextView(this).apply {
            text = value
            textSize = size
            setTextColor(color)
            if (bold) setTypeface(typeface, Typeface.BOLD)
            setPadding(0, 0, 0, 10)
        }

    private fun card(): LinearLayout = LinearLayout(this).apply {
        orientation = LinearLayout.VERTICAL
        setPadding(18, 18, 18, 18)
        setBackgroundColor(surface)
    }

    private fun addCard(view: View) {
        content.addView(view, LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.WRAP_CONTENT
        ).apply { bottomMargin = 14 })
    }

    private fun showDashboard() {
        baseScreen("Dashboard", "Your streaming PC at a glance")
        val connection = card()
        connection.addView(label("WINDOWS COMPANION", 11f, textMuted, true))
        connection.addView(label("Not paired", 21f, textPrimary, true))
        connection.addView(label("Pair with your streaming PC on the same local network to begin.", 13f, textMuted))
        connection.addView(actionButton("PAIR STREAMING PC") { showSettings() })
        addCard(connection)

        val obs = card()
        obs.addView(label("OBS ENCODER OUTPUT", 11f, textMuted, true))
        obs.addView(label("Not connected", 20f, textPrimary, true))
        obs.addView(label("This reports whether OBS is sending the stream to YouTube.", 12f, textMuted))
        addCard(obs)

        val youtube = card()
        youtube.addView(label("YOUTUBE BROADCAST", 11f, textMuted, true))
        youtube.addView(label("Not connected", 20f, textPrimary, true))
        youtube.addView(label("Encoder output and public broadcast status are tracked separately.", 12f, textMuted))
        addCard(youtube)

        val controls = card()
        controls.addView(label("STREAM CONTROLS", 11f, textMuted, true))
        controls.addView(actionButton("START ENCODER OUTPUT", enabled = false) {})
        controls.addView(actionButton("GO LIVE ON YOUTUBE", enabled = false) {})
        controls.addView(actionButton("END BROADCAST", enabled = false) {})
        controls.addView(label("Controls unlock after secure pairing and YouTube authorization.", 12f, textMuted))
        addCard(controls)
    }

    private fun showChat() {
        baseScreen("Live Chat", "YouTube chat, built into your remote")
        val status = card()
        status.addView(label("CHAT CONNECTION", 11f, textMuted, true))
        status.addView(label("Waiting for companion pairing", 17f, textPrimary, true))
        status.addView(label("Messages, Super Chats, Super Stickers, memberships, gifts, polls and supported moderation events will appear here.", 13f, textMuted))
        addCard(status)

        val empty = card()
        empty.gravity = Gravity.CENTER
        empty.addView(label("No messages yet", 19f, textPrimary, true))
        empty.addView(label("Pair your streaming PC and connect your YouTube channel to load the live chat.", 13f, textMuted))
        addCard(empty)

        val compose = card()
        compose.addView(label("SEND A MESSAGE", 11f, textMuted, true))
        val send = actionButton("SEND", enabled = false) {}
        compose.addView(send)
        compose.addView(label("Sending and moderation actions require an authorized YouTube connection.", 12f, textMuted))
        addCard(compose)
    }

    private fun showSettings() {
        baseScreen("Settings", "Connect and secure your streaming setup")
        val pairing = card()
        pairing.addView(label("PAIRING", 11f, textMuted, true))
        pairing.addView(label("Connect to your Windows companion", 18f, textPrimary, true))
        pairing.addView(label("Pairing will use a one-time QR code and a revocable device credential over your private Wi-Fi network.", 13f, textMuted))
        pairing.addView(actionButton("PAIRING NOT AVAILABLE YET", enabled = false) {})
        addCard(pairing)

        val account = card()
        account.addView(label("YOUTUBE ACCOUNT", 11f, textMuted, true))
        account.addView(label("Not authorized", 18f, textPrimary, true))
        account.addView(label("Google sign-in will happen on the Windows companion. Your refresh token will never be stored on this phone.", 13f, textMuted))
        addCard(account)

        val privacy = card()
        privacy.addView(label("NETWORK & PRIVACY", 11f, textMuted, true))
        privacy.addView(label("Local network only", 17f, textPrimary, true))
        privacy.addView(label("No cloud relay, port forwarding or direct internet access to OBS. OBS WebSocket remains on loopback.", 13f, textMuted))
        addCard(privacy)
    }

    private fun actionButton(title: String, enabled: Boolean = true, action: () -> Unit) =
        Button(this).apply {
            text = title
            isEnabled = enabled
            setOnClickListener { action() }
        }
}
