package com.jtg.remoteobs

import android.app.Activity
import android.os.Bundle
import android.view.Gravity
import android.view.ViewGroup
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView

class MainActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(24, 36, 24, 24)
            gravity = Gravity.TOP
            setBackgroundColor(0xFF101216.toInt())
        }

        fun label(text: String, size: Float, color: Int, bold: Boolean = false) =
            TextView(this).apply {
                this.text = text
                textSize = size
                setTextColor(color)
                if (bold) setTypeface(typeface, android.graphics.Typeface.BOLD)
                setPadding(0, 0, 0, 12)
            }

        root.addView(label("JTG REMOTE OBS", 13f, 0xFFE65A55.toInt(), true))
        root.addView(label("Streaming dashboard", 26f, 0xFFF1F3F6.toInt(), true))
        root.addView(label("Companion not paired", 14f, 0xFF9AA4B2.toInt()))

        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(18, 18, 18, 18)
            setBackgroundColor(0xFF1A1E25.toInt())
        }
        card.addView(label("STREAM STATUS", 11f, 0xFF9AA4B2.toInt(), true))
        card.addView(label("Not connected", 18f, 0xFFF1F3F6.toInt(), true))
        card.addView(label("Pair with your streaming PC to get started.", 13f, 0xFF9AA4B2.toInt()))
        root.addView(card, LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.WRAP_CONTENT
        ).apply { bottomMargin = 18 })

        val chat = Button(this).apply {
            text = "LIVE CHAT"
            isEnabled = false
        }
        root.addView(chat)

        root.addView(label(
            "Your gaming PC continues to handle scenes, audio, overlays and NDI. This app controls the streaming PC only.",
            12f,
            0xFF9AA4B2.toInt()
        ))

        setContentView(root)
    }
}
