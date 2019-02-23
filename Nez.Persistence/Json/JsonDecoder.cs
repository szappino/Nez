﻿using System.IO;
using System.Text;
using System;
using JsonArray = System.Collections.Generic.List<object>;
using JsonDict = System.Collections.Generic.Dictionary<string, object>;

namespace Nez.Persistence
{
	public sealed class JsonDecoder : IDisposable
	{
		#region Fields and Props

		const string whiteSpace = " \t\n\r";
		const string wordBreak = " \t\n\r{}[],:\"";
		static readonly StringBuilder _builder = new StringBuilder();

		enum Token
		{
			None,
			OpenBrace,
			CloseBrace,
			OpenBracket,
			CloseBracket,
			Colon,
			Comma,
			String,
			Number,
			True,
			False,
			Null
		}

		StringReader json;

		char PeekChar
		{
			get
			{
				var peek = json.Peek();
				return peek == -1 ? '\0' : Convert.ToChar( peek );
			}
		}

		char NextChar => Convert.ToChar( json.Read() );

		string NextWord
		{
			get
			{
				while( wordBreak.IndexOf( PeekChar ) == -1 )
				{
					_builder.Append( NextChar );

					if( json.Peek() == -1 )
					{
						break;
					}
				}

				var word = _builder.ToString();
				_builder.Length = 0;
				return word;
			}
		}

		Token NextToken
		{
			get
			{
				ConsumeWhiteSpace();

				if( json.Peek() == -1 )
				{
					return Token.None;
				}

				switch( PeekChar )
				{
					case '{':
						return Token.OpenBrace;

					case '}':
						json.Read();
						return Token.CloseBrace;

					case '[':
						return Token.OpenBracket;

					case ']':
						json.Read();
						return Token.CloseBracket;

					case ',':
						json.Read();
						return Token.Comma;

					case '"':
						return Token.String;

					case ':':
						return Token.Colon;

					case '0':
					case '1':
					case '2':
					case '3':
					case '4':
					case '5':
					case '6':
					case '7':
					case '8':
					case '9':
					case '-':
						return Token.Number;
				}

				switch( NextWord )
				{
					case "false":
						return Token.False;

					case "true":
						return Token.True;

					case "null":
						return Token.Null;
				}

				return Token.None;
			}
		}

		#endregion


		public static Variant Decode( string jsonString )
		{
			using( var instance = new JsonDecoder( jsonString ) )
			{
				return instance.DecodeValue();
			}
		}

		JsonDecoder( string jsonString )
		{
			json = new StringReader( jsonString );
		}

		public void Dispose()
		{
			json.Dispose();
			json = null;
		}

		ProxyObject DecodeObject()
		{
			var proxy = new ProxyObject();

			// Ditch opening brace.
			json.Read();

			// {
			while( true )
			{
				switch( NextToken )
				{
					case Token.None:
						return null;
					case Token.Comma:
						continue;
					case Token.CloseBrace:
						return proxy;
					default:
						// Key
						var key = DecodeString();
						if( key == null )
						{
							return null;
						}
						// :
						if( NextToken != Token.Colon )
						{
							return null;
						}

						json.Read();

						// Value
						proxy.Add( key, DecodeValue() );
						break;
				}
			}
		}

		ProxyArray DecodeArray()
		{
			var proxy = new ProxyArray();

			// Ditch opening bracket.
			json.Read();

			// [
			var parsing = true;
			while( parsing )
			{
				var nextToken = NextToken;

				switch( nextToken )
				{
					case Token.None:
						return null;
					case Token.Comma:
						continue;
					case Token.CloseBracket:
						parsing = false;
						break;
					default:
						proxy.Add( DecodeByToken( nextToken ) );
						break;
				}
			}

			return proxy;
		}

		Variant DecodeValue()
		{
			var nextToken = NextToken;
			return DecodeByToken( nextToken );
		}

		Variant DecodeByToken( Token token )
		{
			switch( token )
			{
				case Token.String:
					return DecodeString();
				case Token.Number:
					return DecodeNumber();
				case Token.OpenBrace:
					return DecodeObject();
				case Token.OpenBracket:
					return DecodeArray();
				case Token.True:
					return new ProxyBoolean( true );
				case Token.False:
					return new ProxyBoolean( false );
				case Token.Null:
					return null;
				default:
					return null;
			}
		}

		Variant DecodeString()
		{
			// ditch opening quote
			json.Read();

			var parsing = true;
			while( parsing )
			{
				if( json.Peek() == -1 )
				{
					parsing = false;
					break;
				}

				var c = NextChar;
				switch( c )
				{
					case '"':
						parsing = false;
						break;

					case '\\':
						if( json.Peek() == -1 )
						{
							parsing = false;
							break;
						}

						c = NextChar;

						switch( c )
						{
							case '"':
							case '\\':
							case '/':
								_builder.Append( c );
								break;
							case 'b':
								_builder.Append( '\b' );
								break;
							case 'f':
								_builder.Append( '\f' );
								break;
							case 'n':
								_builder.Append( '\n' );
								break;
							case 'r':
								_builder.Append( '\r' );
								break;
							case 't':
								_builder.Append( '\t' );
								break;
							case 'u':
								var hex = new StringBuilder();

								for( var i = 0; i < 4; i++ )
								{
									hex.Append( NextChar );
								}

								_builder.Append( (char)Convert.ToInt32( hex.ToString(), 16 ) );
								break;

								//default:
								//	throw new DecodeException( @"Illegal character following escape character: " + c );
						}

						break;

					default:
						_builder.Append( c );
						break;
				}
			}

			var str = _builder.ToString();
			_builder.Length = 0;
			return new ProxyString( str );
		}

		Variant DecodeNumber()
		{
			return new ProxyNumber( NextWord );
		}

		void ConsumeWhiteSpace()
		{
			while( whiteSpace.IndexOf( PeekChar ) != -1 )
			{
				json.Read();

				if( json.Peek() == -1 )
				{
					break;
				}
			}
		}

	}
}
